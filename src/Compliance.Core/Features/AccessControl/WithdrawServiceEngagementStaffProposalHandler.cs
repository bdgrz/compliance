using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class WithdrawServiceEngagementStaffProposalHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<WithdrawServiceEngagementStaffProposal, ServiceEngagementView>
{
    public ValueTask<Result<ServiceEngagementView>> HandleAsync(IRequestContext<WithdrawServiceEngagementStaffProposal> context, CancellationToken ct) =>
        executor.ExecuteAsync(new IndependenceLedger(context.Request.TenantId), ledger =>
            AggregateOutcome.CommitOnSuccess(ledger.WithdrawEngagementStaffProposal(context.RequestId,
                context.Request.EngagementId, context.Request.StaffMemberId, context.Request.ExpectedSequence, context.Request.Reason,
                AccessGrantActor.From(context, context.Request.TenantId), clock.GetUtcNow())), context, ct);
}
