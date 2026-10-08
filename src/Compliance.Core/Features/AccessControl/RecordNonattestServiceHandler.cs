using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class RecordNonattestServiceHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<RecordNonattestService, NonattestServiceView>
{
    public ValueTask<Result<NonattestServiceView>> HandleAsync(
        IRequestContext<RecordNonattestService> context, CancellationToken ct) =>
        executor.ExecuteAsync(new IndependenceLedger(context.Request.TenantId), ledger =>
            AggregateOutcome.CommitOnSuccess(ledger.RecordService(context.RequestId, context.Request.ServiceRecordId, context.Request.ExpectedSequence, context.Request.Content,
                AccessGrantActor.From(context, context.Request.TenantId), clock.GetUtcNow())), context, ct);
}
