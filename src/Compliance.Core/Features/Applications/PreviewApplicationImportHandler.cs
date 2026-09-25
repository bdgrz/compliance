using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class PreviewApplicationImportHandler(
    IApplicationImportDirectoryReader directory, ApplicationImportReadConsistency consistency)
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
        Page<ApplicationImportRowView> page;
        try
        {
            page = await directory.ListRowsAsync(request.TenantId, request.BatchId,
                request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ApplicationImportPreviewRow>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The import preview cursor is invalid."));
        }
        if (page.Items.Any(row => row.TenantId != request.TenantId ||
                                  row.BatchId != request.BatchId) ||
            (request.Cursor is null && page.Items.Count == 0 && fresh.Value.RowCount > 0))
            return Result<Page<ApplicationImportPreviewRow>>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The import row projection is incomplete.",
                isTransient: true));
        return Result<Page<ApplicationImportPreviewRow>>.Success(new Page<ApplicationImportPreviewRow>(
            page.Items.Select(row => new ApplicationImportPreviewRow(row.TenantId, row.BatchId,
                    row.RowId, row.RowNumber, row.SourceRecordId, row.Name, row.Purpose,
                    row.OwnerReference, row.ValidationFindings,
                    row.ValidationFindings.Contains("duplicate_source_record_id")
                        ? "duplicate" : row.ValidationFindings.Count > 0 ? "invalid" : "unmatched",
                    [], [], [.. row.ValidationFindings, "source_claims_unavailable"]))
                .ToArray(), page.NextCursor));
    }
}
