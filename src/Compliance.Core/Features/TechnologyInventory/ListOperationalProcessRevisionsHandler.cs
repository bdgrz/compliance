using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

public sealed class ListOperationalProcessRevisionsHandler(IInventoryRegisterReader directory,
    InventoryRegisterReadConsistency consistency)
    : IRequestHandler<ListOperationalProcessRevisions, Page<OperationalProcessView>>
{
    public async ValueTask<Result<Page<OperationalProcessView>>> HandleAsync(
        IRequestContext<ListOperationalProcessRevisions> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<OperationalProcessView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The operational process revision list limit must be between 1 and 200."));
        // Revision rows are written in the same projection batch as the current row.
        var current = await consistency.GetOperationalProcessAsync(request.TenantId, request.OperationalProcessId,
            request.MinimumRevision, ct).ConfigureAwait(false);
        if (!current.IsSuccess)
            return Result<Page<OperationalProcessView>>.Failure(current.Error);
        Page<OperationalProcessView> page;
        try
        {
            page = await directory.ListOperationalProcessRevisionsAsync(request.TenantId, request.OperationalProcessId,
                request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<OperationalProcessView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The operational process revision cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId ||
                                      item.OperationalProcessId != request.OperationalProcessId)
            ? Result<Page<OperationalProcessView>>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The operational process revision projection is incomplete.", isTransient: true))
            : Result<Page<OperationalProcessView>>.Success(page);
    }
}
