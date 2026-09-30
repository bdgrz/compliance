using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

public sealed class ListDataFlowsHandler(ITechnologyInventoryReader directory,
    TechnologyInventoryReadConsistency consistency) : IRequestHandler<ListDataFlows, Page<DataFlowView>>
{
    public async ValueTask<Result<Page<DataFlowView>>> HandleAsync(
        IRequestContext<ListDataFlows> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<DataFlowView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The data flow list limit must be between 1 and 200."));
        var ready = await consistency.EnsureListCaughtUpAsync(request.TenantId, ct)
            .ConfigureAwait(false);
        if (!ready.IsSuccess)
            return Result<Page<DataFlowView>>.Failure(ready.Error);
        Page<DataFlowView> page;
        try
        {
            page = await directory.ListFlowsAsync(request.TenantId, request.Limit ?? 50,
                request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<DataFlowView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The data flow cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId)
            ? Result<Page<DataFlowView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The data flow list was not found."))
            : Result<Page<DataFlowView>>.Success(page);
    }
}
