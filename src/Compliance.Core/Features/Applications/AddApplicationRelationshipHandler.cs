using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class AddApplicationRelationshipHandler(IAggregateExecutor executor,
    IAggregateReader reader, RestrictedApplicationVisibility visibility, TimeProvider clock)
    : IRequestHandler<AddApplicationRelationship, ApplicationRelationshipChange>
{
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
        var source = await reader.HydrateAsync(new DeclaredApplication(request.TenantId,
            request.ApplicationId), ct).ConfigureAwait(false);
        if (!source.IsCreated || !await visibility.CanReadApplicationAsync(request.TenantId,
                userId, request.ApplicationId, ct).ConfigureAwait(false))
            return NotFound();

        if (source.TryGetApplicationRelationship(request.RelationshipId,
                out var existing, out _))
        {
            if (existing is null || !await visibility.CanReadApplicationAsync(request.TenantId,
                    userId, existing.TargetApplicationId, ct).ConfigureAwait(false))
                return NotFound();
            if (source.TryResolveApplicationRelationshipAdd(request.RelationshipId,
                    request.TargetApplicationId, request.RelationshipType, out var retry))
                return retry;
        }
        else if (!await visibility.CanReadApplicationAsync(request.TenantId, userId,
                     request.TargetApplicationId, ct).ConfigureAwait(false))
        {
            return NotFound();
        }

        var (memberId, display) = ApplicationActor.From(context);
        var target = new DeclaredApplication(request.TenantId, request.ApplicationId);
        return await executor.ExecuteAsync(target, app =>
        {
            if (app.TryResolveApplicationRelationshipAdd(request.RelationshipId,
                    request.TargetApplicationId, request.RelationshipType, out var retry))
                return AggregateOutcome.Discard(retry);
            return AggregateOutcome.CommitOnSuccess(app.AddApplicationRelationship(
                request.ExpectedRelationshipRevision, request.RelationshipId,
                request.TargetApplicationId, request.RelationshipType, memberId, display,
                clock.GetUtcNow()));
        }, context, ct).ConfigureAwait(false);
    }

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
