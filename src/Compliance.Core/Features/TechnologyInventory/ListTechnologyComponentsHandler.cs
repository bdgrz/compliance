using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

public sealed class ListTechnologyComponentsHandler(ITechnologyInventoryReader directory,
    TechnologyInventoryReadConsistency consistency) : IRequestHandler<ListTechnologyComponents, Page<TechnologyComponentView>>
{
    public async ValueTask<Result<Page<TechnologyComponentView>>> HandleAsync(
        IRequestContext<ListTechnologyComponents> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<TechnologyComponentView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The technology component list limit must be between 1 and 200."));
        var ready = await consistency.EnsureListCaughtUpAsync(request.TenantId, ct)
            .ConfigureAwait(false);
        if (!ready.IsSuccess)
            return Result<Page<TechnologyComponentView>>.Failure(ready.Error);
        Page<TechnologyComponentView> page;
        try
        {
            page = await directory.ListComponentsAsync(request.TenantId, request.Limit ?? 50,
                request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<TechnologyComponentView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The technology component cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId)
            ? Result<Page<TechnologyComponentView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The technology component list was not found."))
            : Result<Page<TechnologyComponentView>>.Success(page);
    }
}
