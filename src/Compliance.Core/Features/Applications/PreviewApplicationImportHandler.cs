using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class PreviewApplicationImportHandler(
    IApplicationImportDirectoryReader directory, ApplicationImportReadConsistency consistency,
    IAggregateReader reader)
    : IRequestHandler<PreviewApplicationImport, Page<ApplicationImportPreviewRow>>
{
    public async ValueTask<Result<Page<ApplicationImportPreviewRow>>> HandleAsync(
        IRequestContext<PreviewApplicationImport> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ApplicationImportPreviewRow>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The import preview limit must be between 1 and 200."));
        var fresh = await consistency.GetFreshAsync(request.TenantId, request.BatchId,
            request.MinimumRevision, ct).ConfigureAwait(false);
        if (!fresh.IsSuccess)
            return Result<Page<ApplicationImportPreviewRow>>.Failure(fresh.Error);
        var source = await reader.HydrateAsync(new ImportBatch(request.TenantId, request.BatchId), ct).ConfigureAwait(false);
        var ledger = await reader.HydrateAsync(new ApplicationImportLedger(request.TenantId,
            source.SourceKey!, source.SourceNamespace!), ct).ConfigureAwait(false);
        if (ledger.GetRevision(source) != fresh.Value.Revision)
            return Result<Page<ApplicationImportPreviewRow>>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The import lifecycle changed while reading the preview.", isTransient: true));
        var cursorError = ApplicationImportPaging.Decode(request.TenantId, request.BatchId,
            fresh.Value.Revision, request.Cursor, out var rowsCursor);
        if (cursorError is not null)
            return Result<Page<ApplicationImportPreviewRow>>.Failure(cursorError);
        Page<ApplicationImportRowView> page;
        try
        {
            page = await directory.ListRowsAsync(request.TenantId, request.BatchId,
                request.Limit ?? 50, rowsCursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ApplicationImportPreviewRow>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The import preview cursor is invalid."));
        }
        var legacyError = ApplicationImportPaging.CheckLegacyRevision(fresh.Value.Revision, request.Cursor);
        if (legacyError is not null)
            return Result<Page<ApplicationImportPreviewRow>>.Failure(legacyError);
        if (page.Items.Any(row => row.TenantId != request.TenantId ||
                                  row.BatchId != request.BatchId) ||
            (request.Cursor is null && page.Items.Count == 0 && fresh.Value.RowCount > 0))
            return Result<Page<ApplicationImportPreviewRow>>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The import row projection is incomplete.",
                isTransient: true));
        var items = new List<ApplicationImportPreviewRow>();
        foreach (var row in page.Items)
        {
            var choice = ledger.GetCorrelation(request.BatchId, row.RowId);
            var blockers = new List<string>(row.ValidationFindings) { "source_claims_unavailable" };
            if (choice is { Decision: "link_existing" })
            {
                var target = await reader.HydrateAsync(new DeclaredApplication(request.TenantId, choice.ApplicationId), ct)
                    .ConfigureAwait(false);
                if (!target.IsCreated || target.IsRetired || target.Revision != choice.ExpectedApplicationRevision)
                    blockers.Add("correlation_target_changed");
            }
            items.Add(new ApplicationImportPreviewRow(row.TenantId, row.BatchId, row.RowId,
                row.RowNumber, row.SourceRecordId, row.Name, row.Purpose, row.OwnerReference, row.ValidationFindings,
                row.ValidationFindings.Contains("duplicate_source_record_id") ? "duplicate" :
                    row.ValidationFindings.Count > 0 ? "invalid" : "unmatched", [], [], blockers)
            {
                Correlation = choice is not null
                        ? new ApplicationImportCorrelationView(choice.Decision, choice.ApplicationId,
                            choice.ExpectedApplicationRevision, choice.Reason, choice.ActorMemberId,
                            choice.ActorDisplay, choice.RecordedAt, choice.Revision)
                        : null,
            });
        }
        var nextCursor = ApplicationImportPaging.Encode(request.TenantId, request.BatchId,
            fresh.Value.Revision, page.NextCursor);
        return Result<Page<ApplicationImportPreviewRow>>.Success(new Page<ApplicationImportPreviewRow>(items, nextCursor));
    }
}
