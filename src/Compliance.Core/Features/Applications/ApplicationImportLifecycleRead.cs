using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

static class ApplicationImportLifecycleRead
{
    public static async ValueTask<Result<(ImportBatch Batch, ApplicationImportLedger Ledger)>> LoadAsync(
        IAggregateReader reader, Uuid tenantId, Uuid batchId, long? minimumRevision, CancellationToken ct)
    {
        if (minimumRevision is < 1)
            return Result<(ImportBatch, ApplicationImportLedger)>.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum import revision must be positive."));
        var batch = await reader.HydrateAsync(new ImportBatch(tenantId, batchId), ct).ConfigureAwait(false);
        if (!batch.IsCreated || batch.Id != batchId || batch.Stream.Realm != tenantId.ToString() ||
            batch.SourceKey is not { } key || batch.SourceNamespace is not { } space)
            return Result<(ImportBatch, ApplicationImportLedger)>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The import batch was not found."));
        var ledger = await reader.HydrateAsync(new ApplicationImportLedger(tenantId, key, space), ct).ConfigureAwait(false);
        if ((minimumRevision is { } minimum && ledger.GetRevision(batch) < minimum) ||
            (ledger.GetState(batch) == "committed" && !ledger.IsCommitDurable(batchId)))
            return Result<(ImportBatch, ApplicationImportLedger)>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The durable import source has not reached the requested revision.", isTransient: true));
        return Result<(ImportBatch, ApplicationImportLedger)>.Success((batch, ledger));
    }

    public static async ValueTask<Result> CheckAsync(IAggregateReader reader, ImportBatch batch,
        ApplicationImportLedger ledger, CancellationToken ct)
    {
        var tenantId = Uuid.Parse(batch.Stream.Realm, null);
        var current = await reader.HydrateAsync(new ApplicationImportLedger(tenantId,
            batch.SourceKey!, batch.SourceNamespace!), ct).ConfigureAwait(false);
        return current.CommittedStreamPosition == ledger.CommittedStreamPosition &&
               current.GetRevision(batch) == ledger.GetRevision(batch) && current.GetState(batch) == ledger.GetState(batch)
            ? Result.Success : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The import source changed while reading the report; retry.", isTransient: true));
    }
}
