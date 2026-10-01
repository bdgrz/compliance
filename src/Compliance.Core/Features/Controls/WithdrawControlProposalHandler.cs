using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>Withdraws a pending successor draft or retirement proposal at its exact revision.</summary>
public sealed class WithdrawControlProposalHandler(IAggregateExecutor executor,
    ControlLifecycleReleaseGate releaseGate, TimeProvider clock)
    : IRequestHandler<WithdrawControlProposal>
{
    public ValueTask<Result> HandleAsync(IRequestContext<WithdrawControlProposal> context,
        CancellationToken ct)
    {
        if (!releaseGate.IsEnabled)
            return ValueTask.FromResult(Result.Failure(ControlLifecycleReleaseGate.Unavailable));
        var request = context.Request;
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        var memberId = RbacIds.Member(request.TenantId, userId);
        var actor = ActorReference.ForMember(memberId,
            UserIdentityClaims.BdgrzDisplay(context.Actor, userId));
        return executor.ExecuteAsync(new ControlDraft(request.TenantId, request.ControlId),
            control => CommandFailureRequestAdapter.ToOutcome(control.Withdraw(request.ProgramId,
                request.ExpectedRevision, context.RequestId, request.Rationale, memberId, actor,
                clock.GetUtcNow())),
            context, ct);
    }
}
