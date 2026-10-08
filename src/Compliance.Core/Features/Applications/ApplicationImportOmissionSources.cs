using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

static class ApplicationImportOmissionSources
{
    internal static readonly IReadOnlyList<string> Blockers = Array.AsReadOnly(new[]
        { "missing_source_retirement_unavailable", "retirement_impact_unavailable" });

    internal static async ValueTask<Result<(ImportBatch Batch, ApplicationImportLedger Ledger)>> LoadAsync(
        IAggregateReader reader, Uuid tenant, Uuid batchId, CancellationToken ct)
    {
        var batch = await reader.HydrateAsync(new ImportBatch(tenant, batchId), ct).ConfigureAwait(false);
        if (!batch.IsCreated || batch.SourceKey is not { } key || batch.SourceNamespace is not { } space)
            return Result<(ImportBatch, ApplicationImportLedger)>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The import batch was not found."));
        var ledger = await reader.HydrateAsync(new ApplicationImportLedger(tenant, key, space), ct).ConfigureAwait(false);
        return Result<(ImportBatch, ApplicationImportLedger)>.Success((batch, ledger));
    }

    internal static async ValueTask<bool> UnchangedAsync(IAggregateReader reader, Uuid tenant,
        ImportBatch batch, ApplicationImportLedger ledger, CancellationToken ct)
    {
        var latest = await LoadAsync(reader, tenant, batch.Id, ct).ConfigureAwait(false);
        return latest.IsSuccess && latest.Value.Batch.CommittedStreamPosition == batch.CommittedStreamPosition &&
            latest.Value.Batch.ContentDigest == batch.ContentDigest && latest.Value.Batch.SourceKey == batch.SourceKey &&
            latest.Value.Batch.SourceNamespace == batch.SourceNamespace &&
            latest.Value.Ledger.CommittedStreamPosition == ledger.CommittedStreamPosition &&
            latest.Value.Ledger.GetRevision(latest.Value.Batch) == ledger.GetRevision(batch) &&
            latest.Value.Ledger.GetState(latest.Value.Batch) == ledger.GetState(batch);
    }

    internal static RequestError Changed() => new(RequestErrorKind.Conflict,
        "The import source changed while reading omission proposal facts.", isTransient: true);
}
