using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class RemoveApplicationRelationshipHandler(IAggregateExecutor executor,
    IAggregateReader reader, RestrictedApplicationVisibility visibility, TimeProvider clock)
    : IRequestHandler<RemoveApplicationRelationship, ApplicationRelationshipChange>
{
    const int MaximumConcurrencyAttempts = 3;

    public async ValueTask<Result<ApplicationRelationshipChange>> HandleAsync(
        IRequestContext<RemoveApplicationRelationship> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ApplicationId == Uuid.Empty ||
            request.RelationshipId == Uuid.Empty || request.ExpectedRelationshipRevision < 0)
            return Result<ApplicationRelationshipChange>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "A relationship removal requires nonempty IDs and a nonnegative revision."));

        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : Uuid.Empty;
        var (memberId, display) = ApplicationActor.From(context);
        var target = new DeclaredApplication(request.TenantId, request.ApplicationId);
        for (var attempt = 0; attempt < MaximumConcurrencyAttempts; attempt++)
        {
            var source = await reader.HydrateAsync(target, ct).ConfigureAwait(false);
            if (!source.IsCreated || !await visibility.CanReadApplicationAsync(request.TenantId,
                    userId, request.ApplicationId, ct).ConfigureAwait(false) ||
                !source.TryGetApplicationRelationship(request.RelationshipId,
                    out var relationship, out _))
                return NotFound();
            if (relationship is null || !await visibility.CanReadApplicationAsync(
                    request.TenantId, userId, relationship.TargetApplicationId, ct)
                    .ConfigureAwait(false))
                return NotFound();

            var projection = await ApplicationRelationshipProjection.ReadAsync(source,
                request.TenantId, userId, visibility, ct).ConfigureAwait(false);
            if (source.TryResolveApplicationRelationshipRemoval(request.RelationshipId,
                    out var retry))
                return MapVisibleRevision(retry, projection.Revision);

            if (request.ExpectedRelationshipRevision != projection.Revision)
                return Result<ApplicationRelationshipChange>.Failure(
                    ApplicationRelationshipConcurrency.VisibleRevisionMismatch(
                        request.ExpectedRelationshipRevision, projection.Revision));

            var result = await executor.ExecuteAsync(target, app =>
            {
                if (app.SourceRelationshipRevision != source.SourceRelationshipRevision)
                    return AggregateOutcome.Discard(Result<ApplicationRelationshipChange>.Failure(
                        ApplicationRelationshipConcurrency.SourceChanged()));
                if (app.TryResolveApplicationRelationshipRemoval(request.RelationshipId,
                        out var currentRetry))
                    return AggregateOutcome.Discard(currentRetry);
                return AggregateOutcome.CommitOnSuccess(app.RemoveApplicationRelationship(
                    source.SourceRelationshipRevision, request.RelationshipId, memberId,
                    display, clock.GetUtcNow()));
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

    static Result<ApplicationRelationshipChange> NotFound() =>
        Result<ApplicationRelationshipChange>.Failure(new RequestError(RequestErrorKind.NotFound,
            "The application relationship resource was not found."));
}
