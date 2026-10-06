using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class CancelApplicationImportHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<CancelApplicationImport>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<CancelApplicationImport> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var (memberId, display) = ApplicationActor.From(context);
        var batch = await reader.HydrateAsync(new ImportBatch(request.TenantId, request.BatchId), ct)
            .ConfigureAwait(false);
        if (!batch.IsCreated || batch.SourceKey is not { } sourceKey ||
            batch.SourceNamespace is not { } sourceNamespace)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The import batch was not found."));
        return await executor.ExecuteAsync(
            new ApplicationImportLedger(request.TenantId, sourceKey, sourceNamespace),
            ledger => CommandFailureRequestAdapter.ToOutcome(ledger.Cancel(batch,
                request.ExpectedBatchRevision, request.Reason, memberId, display, clock.GetUtcNow())),
            context, ct).ConfigureAwait(false);
    }
}
