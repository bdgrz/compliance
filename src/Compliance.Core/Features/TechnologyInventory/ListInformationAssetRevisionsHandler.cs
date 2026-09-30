using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

public sealed class ListInformationAssetRevisionsHandler(ITechnologyInventoryReader directory,
    TechnologyInventoryReadConsistency consistency) : IRequestHandler<ListInformationAssetRevisions, Page<InformationAssetView>>
{
    public async ValueTask<Result<Page<InformationAssetView>>> HandleAsync(
        IRequestContext<ListInformationAssetRevisions> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<InformationAssetView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The information asset revision list limit must be between 1 and 200."));
        // Revision rows are written in the same projection batch as the current row.
        var current = await consistency.GetAssetAsync(request.TenantId, request.InformationAssetId,
            request.MinimumRevision, ct).ConfigureAwait(false);
        if (!current.IsSuccess)
            return Result<Page<InformationAssetView>>.Failure(current.Error);
        Page<InformationAssetView> page;
        try
        {
            page = await directory.ListAssetRevisionsAsync(request.TenantId, request.InformationAssetId,
                request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<InformationAssetView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The information asset revision cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId ||
                                      item.InformationAssetId != request.InformationAssetId)
            ? Result<Page<InformationAssetView>>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The information asset revision projection is incomplete."))
            : Result<Page<InformationAssetView>>.Success(page);
    }
}
