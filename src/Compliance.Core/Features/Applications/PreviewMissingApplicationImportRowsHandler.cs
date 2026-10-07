using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class PreviewMissingApplicationImportRowsHandler(
    ApplicationImportReadConsistency consistency, IAggregateReader reader)
    : IRequestHandler<PreviewMissingApplicationImportRows, Page<ApplicationImportMissingRow>>
{
    public async ValueTask<Result<Page<ApplicationImportMissingRow>>> HandleAsync(
        IRequestContext<PreviewMissingApplicationImportRows> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ApplicationImportMissingRow>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The missing-row preview limit must be between 1 and 200."));
        var fresh = await consistency.GetFreshAsync(request.TenantId, request.BatchId,
            request.MinimumRevision, ct).ConfigureAwait(false);
        if (!fresh.IsSuccess)
            return Result<Page<ApplicationImportMissingRow>>.Failure(fresh.Error);
        var batch = await reader.HydrateAsync(new ImportBatch(request.TenantId, request.BatchId), ct).ConfigureAwait(false);
        var ledger = await reader.HydrateAsync(new ApplicationImportLedger(request.TenantId,
            batch.SourceKey!, batch.SourceNamespace!), ct).ConfigureAwait(false);
        if (ledger.GetRevision(batch) != fresh.Value.Revision)
            return Result<Page<ApplicationImportMissingRow>>.Failure(Changed());
        var missing = ledger.GetMissingSourceClaims(batch);
        if (!missing.IsSuccess)
            return Result<Page<ApplicationImportMissingRow>>.Failure(missing.Error);
        var position = ledger.CommittedStreamPosition;
        var decoded = ApplicationImportMissingPaging.Decode(request.TenantId, request.BatchId,
            fresh.Value.Revision, position, request.Cursor);
        if (!decoded.IsSuccess)
            return Result<Page<ApplicationImportMissingRow>>.Failure(decoded.Error);
        var offset = decoded.Value;
        if (request.Cursor is not null && offset >= missing.Value.Count)
            return Result<Page<ApplicationImportMissingRow>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The missing-row preview cursor is outside its source claims."));
        var limit = request.Limit ?? 50;
        var items = new List<ApplicationImportMissingRow>();
        foreach (var claim in missing.Value.Skip(offset).Take(limit))
        {
            var target = await reader.HydrateApplicationAsync(request.TenantId, claim.Observation.ApplicationId, ct)
                .ConfigureAwait(false);
            var expected = claim.Observation.ExpectedApplicationRevision ?? 1;
            var blockers = new List<string> { "missing_source_retirement_unavailable", "retirement_impact_unavailable" };
            if (!target.IsCreated || target.IsRetired || target.Id != claim.Observation.ApplicationId ||
                target.Stream.Realm != request.TenantId.ToString() || target.Revision != expected)
                blockers.Add("source_claim_target_changed");
            if (ledger.GetState(batch) == "canceled")
                blockers.Add("batch_canceled");
            items.Add(new ApplicationImportMissingRow(request.TenantId, request.BatchId,
                Uuid.CreateVersion5(ledger.Id, $"application_import_claim:{claim.Observation.SourceRecordId}"),
                claim.Observation.SourceRecordId, claim.Observation.ApplicationId, claim.Plan.BatchId,
                claim.Observation.Name, claim.Observation.Purpose, claim.Observation.OwnerReference,
                claim.CommittedAt, expected,
                target.IsCreated && target.Id == claim.Observation.ApplicationId && target.Stream.Realm == request.TenantId.ToString()
                    ? target.Revision : null,
                "missing_from_source", blockers));
        }
        var latestBatch = await reader.HydrateAsync(new ImportBatch(request.TenantId, request.BatchId), ct).ConfigureAwait(false);
        var latestLedger = await reader.HydrateAsync(new ApplicationImportLedger(request.TenantId,
            batch.SourceKey!, batch.SourceNamespace!), ct).ConfigureAwait(false);
        if (latestBatch.SourceKey != batch.SourceKey || latestBatch.SourceNamespace != batch.SourceNamespace ||
            latestLedger.CommittedStreamPosition != position || latestLedger.GetRevision(latestBatch) != fresh.Value.Revision ||
            !latestLedger.GetMissingSourceClaims(latestBatch).IsSuccess)
            return Result<Page<ApplicationImportMissingRow>>.Failure(Changed());
        var nextOffset = offset + items.Count;
        var nextCursor = nextOffset < missing.Value.Count
            ? ApplicationImportMissingPaging.Encode(request.TenantId, request.BatchId, fresh.Value.Revision, position, nextOffset)
            : null;
        return Result<Page<ApplicationImportMissingRow>>.Success(new(items, nextCursor));
    }

    static RequestError Changed() => new(RequestErrorKind.Conflict,
        "The import source changed while reading missing-row proposals.", isTransient: true);
}
