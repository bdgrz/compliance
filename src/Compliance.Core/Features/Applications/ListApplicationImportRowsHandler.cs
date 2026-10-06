using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class ListApplicationImportRowsHandler(
    IApplicationImportDirectoryReader directory, ApplicationImportReadConsistency consistency)
    : IRequestHandler<ListApplicationImportRows, Page<ApplicationImportRowView>>
{
    public async ValueTask<Result<Page<ApplicationImportRowView>>> HandleAsync(
        IRequestContext<ListApplicationImportRows> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ApplicationImportRowView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The import row limit must be between 1 and 200."));
        var fresh = await consistency.GetFreshAsync(request.TenantId, request.BatchId,
            request.MinimumRevision, ct).ConfigureAwait(false);
        if (!fresh.IsSuccess)
            return Result<Page<ApplicationImportRowView>>.Failure(fresh.Error);
        var cursorError = ApplicationImportPaging.Decode(request.TenantId, request.BatchId,
            fresh.Value.Revision, request.Cursor, out var rowsCursor);
        if (cursorError is not null)
            return Result<Page<ApplicationImportRowView>>.Failure(cursorError);
        Page<ApplicationImportRowView> page;
        try
        {
            page = await directory.ListRowsAsync(request.TenantId, request.BatchId,
                request.Limit ?? 50, rowsCursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ApplicationImportRowView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The import row cursor is invalid."));
        }
        var legacyError = ApplicationImportPaging.CheckLegacyRevision(fresh.Value.Revision, request.Cursor);
        if (legacyError is not null)
            return Result<Page<ApplicationImportRowView>>.Failure(legacyError);
        if (page.Items.Any(row => row.TenantId != request.TenantId ||
                                  row.BatchId != request.BatchId) ||
            (request.Cursor is null && page.Items.Count == 0 && fresh.Value.RowCount > 0))
            return Result<Page<ApplicationImportRowView>>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The import row projection is incomplete.",
                isTransient: true));
        return Result<Page<ApplicationImportRowView>>.Success(new Page<ApplicationImportRowView>(page.Items,
            ApplicationImportPaging.Encode(request.TenantId, request.BatchId, fresh.Value.Revision, page.NextCursor)));
    }
}
