using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class CloseServiceEngagementHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<CloseServiceEngagement, ServiceEngagementView>
{
    public ValueTask<Result<ServiceEngagementView>> HandleAsync(IRequestContext<CloseServiceEngagement> context, CancellationToken ct) =>
        executor.ExecuteAsync(new IndependenceLedger(context.Request.TenantId), ledger =>
            AggregateOutcome.CommitOnSuccess(ledger.CloseEngagement(context.RequestId,
                context.Request.EngagementId, context.Request.ExpectedSequence, context.Request.Reason,
                AccessGrantActor.From(context, context.Request.TenantId), clock.GetUtcNow())), context, ct);
}
