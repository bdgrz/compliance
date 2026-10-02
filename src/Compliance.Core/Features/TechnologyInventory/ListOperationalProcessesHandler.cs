using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

public sealed class ListOperationalProcessesHandler(IInventoryRegisterReader directory,
    InventoryRegisterReadConsistency consistency)
    : IRequestHandler<ListOperationalProcesses, Page<OperationalProcessView>>
{
    public async ValueTask<Result<Page<OperationalProcessView>>> HandleAsync(
        IRequestContext<ListOperationalProcesses> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<OperationalProcessView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The operational process list limit must be between 1 and 200."));
        var ready = await consistency.EnsureListCaughtUpAsync(request.TenantId, ct)
            .ConfigureAwait(false);
        if (!ready.IsSuccess)
            return Result<Page<OperationalProcessView>>.Failure(ready.Error);
        Page<OperationalProcessView> page;
        try
        {
            page = await directory.ListOperationalProcessesAsync(request.TenantId, request.Limit ?? 50,
                request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<OperationalProcessView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The operational process cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId)
            ? Result<Page<OperationalProcessView>>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The operational process projection is incomplete.", isTransient: true))
            : Result<Page<OperationalProcessView>>.Success(page);
    }
}
