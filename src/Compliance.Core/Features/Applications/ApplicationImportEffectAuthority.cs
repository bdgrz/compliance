using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

static class ApplicationImportEffectAuthority
{
    public static async ValueTask<Result<(ImportBatch Batch, ApplicationImportLedger Ledger)>> LoadAsync(
        IAggregateReader reader, ApplyApplicationImportEffect request, CancellationToken ct)
    {
        var batch = await reader.HydrateAsync(new ImportBatch(request.TenantId, request.BatchId), ct)
            .ConfigureAwait(false);
        if (!batch.IsCreated || batch.SourceKey is not { } sourceKey || batch.SourceNamespace is not { } sourceNamespace)
            return Result<(ImportBatch, ApplicationImportLedger)>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The import batch was not found."));
        var ledger = await reader.HydrateAsync(new ApplicationImportLedger(request.TenantId, sourceKey, sourceNamespace), ct)
            .ConfigureAwait(false);
        var authority = ledger.AuthorizePendingEffect(batch, request.RowId, request.ApplicationId);
        return authority.IsSuccess
            ? Result<(ImportBatch, ApplicationImportLedger)>.Success((batch, ledger))
            : Result<(ImportBatch, ApplicationImportLedger)>.Failure(authority.Error);
    }
}
