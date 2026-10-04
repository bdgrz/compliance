using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

public sealed class ListInformationAssetRevisionsHandler(IAggregateReader aggregates,
    ITechnologyInventoryReader directory,
    TechnologyInventoryReadConsistency consistency, TechnologyInventoryRestrictedVisibility visibility)
    : IRequestHandler<ListInformationAssetRevisions, Page<InformationAssetView>>
{
    public async ValueTask<Result<Page<InformationAssetView>>> HandleAsync(
        IRequestContext<ListInformationAssetRevisions> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<InformationAssetView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The information asset revision list limit must be between 1 and 200."));
        if (request.MinimumRevision is < 1)
            return Result<Page<InformationAssetView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The minimum information asset revision must be positive."));
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : Uuid.Empty;
        var source = await aggregates.HydrateAsync(new InformationAsset(request.TenantId,
            request.InformationAssetId), ct).ConfigureAwait(false);
        if (!source.IsCreated || !await visibility.CanReadAssetAsync(request.TenantId, userId,
                source.Id, source.Content?.Classification, ct).ConfigureAwait(false))
            return Result<Page<InformationAssetView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The information asset was not found."));
        var fence = await consistency.CaptureAsync(request.TenantId, ct).ConfigureAwait(false);
        if (!fence.IsSuccess)
            return Result<Page<InformationAssetView>>.Failure(fence.Error);
        var current = await consistency.GetAssetAsync(request.TenantId, request.InformationAssetId,
            request.MinimumRevision, ct).ConfigureAwait(false);
        if (!current.IsSuccess)
            return Result<Page<InformationAssetView>>.Failure(current.Error);
        if (!await visibility.CanReadAssetAsync(request.TenantId, userId, current.Value, ct)
                .ConfigureAwait(false))
            return Result<Page<InformationAssetView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The information asset was not found."));
        Page<InformationAssetView> page;
        try
        {
            page = await VisibleTechnologyInventoryPage.ReadAsync(request.Limit ?? 50,
                request.Cursor,
                (limit, cursor) => directory.ListAssetRevisionsAsync(request.TenantId,
                    request.InformationAssetId, limit, cursor, ct),
                item => visibility.CanReadAssetAsync(request.TenantId, userId, item, ct),
                item => item.TenantId == request.TenantId &&
                        item.InformationAssetId == request.InformationAssetId)
                .ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<InformationAssetView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The information asset revision cursor is invalid."));
        }
        catch (VisibleTechnologyInventoryPage.ForeignDirectoryItemException)
        {
            return Result<Page<InformationAssetView>>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The information asset revision projection is incomplete."));
        }
        var unchanged = await consistency.ConfirmUnchangedAndCaughtUpAsync(request.TenantId,
            fence.Value, ct).ConfigureAwait(false);
        return unchanged.IsSuccess
            ? Result<Page<InformationAssetView>>.Success(page)
            : Result<Page<InformationAssetView>>.Failure(unchanged.Error);
    }
}
