using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class RevokeServiceEngagementActualStaffHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<RevokeServiceEngagementActualStaff, ServiceEngagementAcceptanceView>
{
    public ValueTask<Result<ServiceEngagementAcceptanceView>> HandleAsync(
        IRequestContext<RevokeServiceEngagementActualStaff> context, CancellationToken ct) =>
        executor.ExecuteAsync(new IndependenceLedger(context.Request.TenantId), ledger =>
            AggregateOutcome.CommitOnSuccess(ledger.RemoveActualStaff(context.RequestId,
                context.Request.EngagementId, context.Request.StaffMemberId, context.Request.ExpectedSequence,
                context.Request.Reason, AccessGrantActor.From(context, context.Request.TenantId), clock.GetUtcNow())),
            context, ct);
}
