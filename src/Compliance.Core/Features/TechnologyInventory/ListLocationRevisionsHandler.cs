using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

public sealed class ListLocationRevisionsHandler(IInventoryRegisterReader directory,
    InventoryRegisterReadConsistency consistency)
    : IRequestHandler<ListLocationRevisions, Page<LocationView>>
{
    public async ValueTask<Result<Page<LocationView>>> HandleAsync(
        IRequestContext<ListLocationRevisions> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<LocationView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The location revision list limit must be between 1 and 200."));
        // Revision rows are written in the same projection batch as the current row.
        var current = await consistency.GetLocationAsync(request.TenantId, request.LocationId,
            request.MinimumRevision, ct).ConfigureAwait(false);
        if (!current.IsSuccess)
            return Result<Page<LocationView>>.Failure(current.Error);
        Page<LocationView> page;
        try
        {
            page = await directory.ListLocationRevisionsAsync(request.TenantId, request.LocationId,
                request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<LocationView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The location revision cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId ||
                                      item.LocationId != request.LocationId)
            ? Result<Page<LocationView>>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The location revision projection is incomplete.", isTransient: true))
            : Result<Page<LocationView>>.Success(page);
    }
}
