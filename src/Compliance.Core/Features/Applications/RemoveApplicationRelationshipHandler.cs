using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class RemoveApplicationRelationshipHandler(IAggregateExecutor executor,
    IAggregateReader reader, RestrictedApplicationVisibility visibility, TimeProvider clock)
    : IRequestHandler<RemoveApplicationRelationship, ApplicationRelationshipChange>
{
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
        var source = await reader.HydrateAsync(new DeclaredApplication(request.TenantId,
            request.ApplicationId), ct).ConfigureAwait(false);
        if (!source.IsCreated || !await visibility.CanReadApplicationAsync(request.TenantId,
                userId, request.ApplicationId, ct).ConfigureAwait(false) ||
            !source.TryGetApplicationRelationship(request.RelationshipId,
                out var relationship, out _))
            return NotFound();
        if (relationship is null || !await visibility.CanReadApplicationAsync(request.TenantId,
                userId, relationship.TargetApplicationId, ct).ConfigureAwait(false))
            return NotFound();
        if (source.TryResolveApplicationRelationshipRemoval(request.RelationshipId,
                out var retry))
            return retry;

        var (memberId, display) = ApplicationActor.From(context);
        var target = new DeclaredApplication(request.TenantId, request.ApplicationId);
        return await executor.ExecuteAsync(target, app =>
        {
            if (app.TryResolveApplicationRelationshipRemoval(request.RelationshipId,
                    out var currentRetry))
                return AggregateOutcome.Discard(currentRetry);
            return AggregateOutcome.CommitOnSuccess(app.RemoveApplicationRelationship(
                request.ExpectedRelationshipRevision, request.RelationshipId, memberId,
                display, clock.GetUtcNow()));
        }, context, ct).ConfigureAwait(false);
    }

    static Result<ApplicationRelationshipChange> NotFound() =>
        Result<ApplicationRelationshipChange>.Failure(new RequestError(RequestErrorKind.NotFound,
            "The application relationship resource was not found."));
}
