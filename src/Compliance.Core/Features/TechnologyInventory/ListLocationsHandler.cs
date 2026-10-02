using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

public sealed class ListLocationsHandler(IInventoryRegisterReader directory,
    InventoryRegisterReadConsistency consistency)
    : IRequestHandler<ListLocations, Page<LocationView>>
{
    public async ValueTask<Result<Page<LocationView>>> HandleAsync(
        IRequestContext<ListLocations> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<LocationView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The location list limit must be between 1 and 200."));
        var ready = await consistency.EnsureListCaughtUpAsync(request.TenantId, ct)
            .ConfigureAwait(false);
        if (!ready.IsSuccess)
            return Result<Page<LocationView>>.Failure(ready.Error);
        Page<LocationView> page;
        try
        {
            page = await directory.ListLocationsAsync(request.TenantId, request.Limit ?? 50,
                request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<LocationView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The location cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId)
            ? Result<Page<LocationView>>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The location projection is incomplete.", isTransient: true))
            : Result<Page<LocationView>>.Success(page);
    }
}
