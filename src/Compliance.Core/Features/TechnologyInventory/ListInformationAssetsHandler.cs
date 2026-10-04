using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

public sealed class ListInformationAssetsHandler(ITechnologyInventoryReader directory,
    TechnologyInventoryReadConsistency consistency, TechnologyInventoryRestrictedVisibility visibility)
    : IRequestHandler<ListInformationAssets, Page<InformationAssetView>>
{
    public async ValueTask<Result<Page<InformationAssetView>>> HandleAsync(
        IRequestContext<ListInformationAssets> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<InformationAssetView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The information asset list limit must be between 1 and 200."));
        var fence = await consistency.CaptureAsync(request.TenantId, ct).ConfigureAwait(false);
        if (!fence.IsSuccess)
            return Result<Page<InformationAssetView>>.Failure(fence.Error);
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : Uuid.Empty;
        Page<InformationAssetView> page;
        try
        {
            page = await VisibleTechnologyInventoryPage.ReadAsync(request.Limit ?? 50,
                request.Cursor,
                (limit, cursor) => directory.ListAssetsAsync(request.TenantId, limit, cursor, ct),
                item => visibility.CanReadAssetAsync(request.TenantId, userId, item, ct),
                item => item.TenantId == request.TenantId && item.InformationAssetId != Uuid.Empty)
                .ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<InformationAssetView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The information asset cursor is invalid."));
        }
        catch (VisibleTechnologyInventoryPage.ForeignDirectoryItemException)
        {
            return Result<Page<InformationAssetView>>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The information asset list was not found."));
        }
        var current = await consistency.ConfirmUnchangedAndCaughtUpAsync(request.TenantId,
            fence.Value, ct).ConfigureAwait(false);
        return current.IsSuccess
            ? Result<Page<InformationAssetView>>.Success(page)
            : Result<Page<InformationAssetView>>.Failure(current.Error);
    }
}
