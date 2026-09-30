using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

public sealed class ListTechnologyComponentRevisionsHandler(ITechnologyInventoryReader directory,
    TechnologyInventoryReadConsistency consistency) : IRequestHandler<ListTechnologyComponentRevisions, Page<TechnologyComponentView>>
{
    public async ValueTask<Result<Page<TechnologyComponentView>>> HandleAsync(
        IRequestContext<ListTechnologyComponentRevisions> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<TechnologyComponentView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The technology component revision list limit must be between 1 and 200."));
        // Revision rows are written in the same projection batch as the current row.
        var current = await consistency.GetComponentAsync(request.TenantId, request.ComponentId,
            request.MinimumRevision, ct).ConfigureAwait(false);
        if (!current.IsSuccess)
            return Result<Page<TechnologyComponentView>>.Failure(current.Error);
        Page<TechnologyComponentView> page;
        try
        {
            page = await directory.ListComponentRevisionsAsync(request.TenantId, request.ComponentId,
                request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<TechnologyComponentView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The technology component revision cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId ||
                                      item.ComponentId != request.ComponentId)
            ? Result<Page<TechnologyComponentView>>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The technology component revision projection is incomplete."))
            : Result<Page<TechnologyComponentView>>.Success(page);
    }
}
