using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class AddApplicationRelationshipHandler(IAggregateExecutor executor,
    IAggregateReader reader, RestrictedApplicationVisibility visibility, TimeProvider clock)
    : IRequestHandler<AddApplicationRelationship, ApplicationRelationshipChange>
{
    const int MaximumConcurrencyAttempts = 3;

    public async ValueTask<Result<ApplicationRelationshipChange>> HandleAsync(
        IRequestContext<AddApplicationRelationship> context, CancellationToken ct)
    {
        var request = context.Request;
        var invalid = Validate(request);
        if (invalid is not null)
            return Result<ApplicationRelationshipChange>.Failure(invalid);

        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : Uuid.Empty;
        var (memberId, display) = ApplicationActor.From(context);
        var target = new DeclaredApplication(request.TenantId, request.ApplicationId);
        for (var attempt = 0; attempt < MaximumConcurrencyAttempts; attempt++)
        {
            var source = await reader.HydrateAsync(target, ct).ConfigureAwait(false);
            if (!source.IsCreated || !await visibility.CanReadApplicationAsync(request.TenantId,
                    userId, request.ApplicationId, ct).ConfigureAwait(false))
                return NotFound();

            var projection = await ApplicationRelationshipProjection.ReadAsync(source,
                request.TenantId, userId, visibility, ct).ConfigureAwait(false);
            if (source.TryGetApplicationRelationship(request.RelationshipId,
                    out var existing, out _))
            {
                if (existing is null || !await visibility.CanReadApplicationAsync(
                        request.TenantId, userId, existing.TargetApplicationId, ct)
                        .ConfigureAwait(false))
                    return NotFound();
                if (source.TryResolveApplicationRelationshipAdd(request.RelationshipId,
                        request.TargetApplicationId, request.RelationshipType, out var retry))
                    return MapVisibleRevision(retry, projection.Revision);
            }
            else if (!await visibility.CanReadApplicationAsync(request.TenantId, userId,
                         request.TargetApplicationId, ct).ConfigureAwait(false))
            {
                return NotFound();
            }

            if (request.ExpectedRelationshipRevision != projection.Revision)
                return Result<ApplicationRelationshipChange>.Failure(
                    ApplicationRelationshipConcurrency.VisibleRevisionMismatch(
                        request.ExpectedRelationshipRevision, projection.Revision));

            var result = await executor.ExecuteAsync(target, app =>
            {
                if (app.SourceRelationshipRevision != source.SourceRelationshipRevision)
                    return AggregateOutcome.Discard(Result<ApplicationRelationshipChange>.Failure(
                        ApplicationRelationshipConcurrency.SourceChanged()));
                if (app.TryResolveApplicationRelationshipAdd(request.RelationshipId,
                        request.TargetApplicationId, request.RelationshipType, out var retry))
                    return AggregateOutcome.Discard(retry);
                return AggregateOutcome.CommitOnSuccess(app.AddApplicationRelationship(
                    source.SourceRelationshipRevision, request.RelationshipId,
                    request.TargetApplicationId, request.RelationshipType, memberId, display,
                    clock.GetUtcNow()));
            }, context, ct).ConfigureAwait(false);
            if (ApplicationRelationshipConcurrency.IsSourceChanged(result.Error))
                continue;
            if (!result.IsSuccess)
                return result;

            var latest = await reader.HydrateAsync(target, ct).ConfigureAwait(false);
            var latestProjection = await ApplicationRelationshipProjection.ReadAsync(latest,
                request.TenantId, userId, visibility, ct).ConfigureAwait(false);
            return MapVisibleRevision(result, latestProjection.Revision);
        }

        return Result<ApplicationRelationshipChange>.Failure(
            ApplicationRelationshipConcurrency.SourceChanged());
    }

    static Result<ApplicationRelationshipChange> MapVisibleRevision(
        Result<ApplicationRelationshipChange> result, long revision) => result.IsSuccess
        ? Result<ApplicationRelationshipChange>.Success(result.Value with
        {
            RelationshipRevision = revision,
        })
        : result;

    static RequestError? Validate(AddApplicationRelationship request)
    {
        if (request.TenantId == Uuid.Empty || request.ApplicationId == Uuid.Empty ||
            request.RelationshipId == Uuid.Empty || request.TargetApplicationId == Uuid.Empty ||
            request.ApplicationId == request.TargetApplicationId ||
            request.RelationshipType is not "depends_on" and not "replaces" ||
            request.ExpectedRelationshipRevision < 0)
            return new RequestError(RequestErrorKind.Validation,
                "A relationship requires nonempty distinct IDs, a supported type and a nonnegative revision.");
        return null;
    }

    static Result<ApplicationRelationshipChange> NotFound() =>
        Result<ApplicationRelationshipChange>.Failure(new RequestError(RequestErrorKind.NotFound,
            "The application relationship resource was not found."));
}
