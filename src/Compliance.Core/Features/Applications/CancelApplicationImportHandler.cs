using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class CancelApplicationImportHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<CancelApplicationImport>
{
    public ValueTask<Result> HandleAsync(IRequestContext<CancelApplicationImport> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var (memberId, display) = ApplicationActor.From(context);
        return executor.ExecuteAsync(new ImportBatch(request.TenantId, request.BatchId),
            batch => AggregateOutcome.CommitOnSuccess(batch.Cancel(request.ExpectedBatchRevision,
                request.Reason, memberId, display, clock.GetUtcNow())), context, ct);
    }
}
