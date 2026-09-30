using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

public sealed class ListInformationAssetsHandler(ITechnologyInventoryReader directory,
    TechnologyInventoryReadConsistency consistency) : IRequestHandler<ListInformationAssets, Page<InformationAssetView>>
{
    public async ValueTask<Result<Page<InformationAssetView>>> HandleAsync(
        IRequestContext<ListInformationAssets> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<InformationAssetView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The information asset list limit must be between 1 and 200."));
        var ready = await consistency.EnsureListCaughtUpAsync(request.TenantId, ct)
            .ConfigureAwait(false);
        if (!ready.IsSuccess)
            return Result<Page<InformationAssetView>>.Failure(ready.Error);
        Page<InformationAssetView> page;
        try
        {
            page = await directory.ListAssetsAsync(request.TenantId, request.Limit ?? 50,
                request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<InformationAssetView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The information asset cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId)
            ? Result<Page<InformationAssetView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The information asset list was not found."))
            : Result<Page<InformationAssetView>>.Success(page);
    }
}
