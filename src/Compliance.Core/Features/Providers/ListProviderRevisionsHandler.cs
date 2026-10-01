using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

public sealed class ListProviderRevisionsHandler(IProviderReader directory, ProviderReadConsistency consistency)
    : IRequestHandler<ListProviderRevisions, Page<ProviderView>>
{
    public async ValueTask<Result<Page<ProviderView>>> HandleAsync(IRequestContext<ListProviderRevisions> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ProviderView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The provider revision list limit must be between 1 and 200."));
        var current = await consistency.GetAsync(request.TenantId, request.ProviderId, null, ct).ConfigureAwait(false);
        if (!current.IsSuccess)
            return Result<Page<ProviderView>>.Failure(current.Error);
        Page<ProviderView> page;
        try
        {
            page = await directory.ListRevisionsAsync(request.TenantId, request.ProviderId,
                request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ProviderView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The provider revision cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId || item.ProviderId != request.ProviderId ||
            item.Revision < 1 || item.Revision > current.Value.Revision)
            ? Result<Page<ProviderView>>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The provider revision projection is incomplete."))
            : Result<Page<ProviderView>>.Success(page);
    }
}
