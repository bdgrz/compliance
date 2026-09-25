using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public sealed class ReviewBoundaryHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<ReviewBoundary>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviewBoundary> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        return executor.ExecuteAsync(new SystemBoundary(request.TenantId, request.BoundaryId),
            boundary => CommandFailureRequestAdapter.ToOutcome(boundary.Review(request.DraftVersionId,
                request.ExpectedRevision, context.RequestId, request.Outcome, request.Rationale,
                RbacIds.Member(request.TenantId, userId),
                UserIdentityClaims.BdgrzDisplay(context.Actor, userId), clock.GetUtcNow())),
            context, ct);
    }
}
