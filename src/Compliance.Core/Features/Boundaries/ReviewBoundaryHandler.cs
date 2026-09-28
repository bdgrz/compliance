using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public sealed class ReviewBoundaryHandler(IAggregateExecutor executor, IAggregateReader reader,
    TimeProvider clock)
    : IRequestHandler<ReviewBoundary>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ReviewBoundary> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        var memberId = RbacIds.Member(request.TenantId, userId);
        var now = clock.GetUtcNow();
        var waiverId = request.SeparationOfDutiesWaiverId;
        SeparationOfDutiesWaiver? waiver = null;
        if (waiverId is not null)
        {
            waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId,
                waiverId.Value), ct).ConfigureAwait(false);
        }
        return await executor.ExecuteAsync(new SystemBoundary(request.TenantId, request.BoundaryId),
            boundary => CommandFailureRequestAdapter.ToOutcome(boundary.Review(request.DraftVersionId,
                request.ExpectedRevision, context.RequestId, request.Outcome, request.Rationale,
                memberId, UserIdentityClaims.BdgrzDisplay(context.Actor, userId), now, waiver)),
            context, ct).ConfigureAwait(false);
    }
}
