using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public sealed class ReviewControlHandler(IAggregateExecutor executor, IAggregateReader reader,
    ControlActivationReleaseGate releaseGate, TimeProvider clock)
    : IRequestHandler<ReviewControl>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ReviewControl> context,
        CancellationToken ct)
    {
        if (!releaseGate.IsEnabled)
            return Result.Failure(ControlActivationReleaseGate.Unavailable);
        var request = context.Request;
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        SeparationOfDutiesWaiver? waiver = null;
        if (request.SeparationOfDutiesWaiverId is { } waiverId)
            waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId,
                waiverId), ct).ConfigureAwait(false);
        return await executor.ExecuteAsync(new ControlDraft(request.TenantId, request.ControlId),
            control => CommandFailureRequestAdapter.ToOutcome(control.Review(request.ProgramId,
                request.ExpectedRevision, context.RequestId, request.Outcome, request.Rationale,
                RbacIds.Member(request.TenantId, userId),
                UserIdentityClaims.BdgrzDisplay(context.Actor, userId), clock.GetUtcNow(),
                waiver)),
            context, ct).ConfigureAwait(false);
    }
}
