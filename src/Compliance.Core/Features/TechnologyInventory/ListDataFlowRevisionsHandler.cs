using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

public sealed class ListDataFlowRevisionsHandler(ITechnologyInventoryReader directory,
    TechnologyInventoryReadConsistency consistency) : IRequestHandler<ListDataFlowRevisions, Page<DataFlowView>>
{
    public async ValueTask<Result<Page<DataFlowView>>> HandleAsync(
        IRequestContext<ListDataFlowRevisions> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<DataFlowView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The data flow revision list limit must be between 1 and 200."));
        // Revision rows are written in the same projection batch as the current row.
        var current = await consistency.GetFlowAsync(request.TenantId, request.DataFlowId,
            request.MinimumRevision, ct).ConfigureAwait(false);
        if (!current.IsSuccess)
            return Result<Page<DataFlowView>>.Failure(current.Error);
        Page<DataFlowView> page;
        try
        {
            page = await directory.ListFlowRevisionsAsync(request.TenantId, request.DataFlowId,
                request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<DataFlowView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The data flow revision cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId ||
                                      item.DataFlowId != request.DataFlowId)
            ? Result<Page<DataFlowView>>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The data flow revision projection is incomplete."))
            : Result<Page<DataFlowView>>.Success(page);
    }
}
