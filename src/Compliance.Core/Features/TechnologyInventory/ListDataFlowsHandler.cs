using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

public sealed class ListDataFlowsHandler(ITechnologyInventoryReader directory,
    TechnologyInventoryReadConsistency consistency, TechnologyInventoryRestrictedVisibility visibility)
    : IRequestHandler<ListDataFlows, Page<DataFlowView>>
{
    public async ValueTask<Result<Page<DataFlowView>>> HandleAsync(
        IRequestContext<ListDataFlows> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<DataFlowView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The data flow list limit must be between 1 and 200."));
        var fence = await consistency.CaptureAsync(request.TenantId, ct).ConfigureAwait(false);
        if (!fence.IsSuccess)
            return Result<Page<DataFlowView>>.Failure(fence.Error);
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : Uuid.Empty;
        Page<DataFlowView> page;
        try
        {
            page = await VisibleTechnologyInventoryPage.ReadAsync(request.Limit ?? 50,
                request.Cursor,
                (limit, cursor) => directory.ListFlowsAsync(request.TenantId, limit, cursor, ct),
                item => visibility.CanReadFlowAsync(request.TenantId, userId, item, ct),
                item => item.TenantId == request.TenantId && item.DataFlowId != Uuid.Empty)
                .ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<DataFlowView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The data flow cursor is invalid."));
        }
        catch (VisibleTechnologyInventoryPage.ForeignDirectoryItemException)
        {
            return Result<Page<DataFlowView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The data flow list was not found."));
        }
        var current = await consistency.ConfirmUnchangedAndCaughtUpAsync(request.TenantId,
            fence.Value, ct).ConfigureAwait(false);
        return current.IsSuccess
            ? Result<Page<DataFlowView>>.Success(page)
            : Result<Page<DataFlowView>>.Failure(current.Error);
    }
}
