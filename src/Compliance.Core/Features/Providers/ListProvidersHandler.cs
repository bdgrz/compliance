using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

public sealed class ListProvidersHandler(IProviderReader directory, ProviderReadConsistency consistency)
    : IRequestHandler<ListProviders, Page<ProviderView>>
{
    public async ValueTask<Result<Page<ProviderView>>> HandleAsync(IRequestContext<ListProviders> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ProviderView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The provider list limit must be between 1 and 200."));
        var freshness = await consistency.EnsureListCaughtUpAsync(request.TenantId, ct).ConfigureAwait(false);
        if (!freshness.IsSuccess)
            return Result<Page<ProviderView>>.Failure(freshness.Error);
        Page<ProviderView> page;
        try
        {
            page = await directory.ListAsync(request.TenantId, request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ProviderView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The provider cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId)
            ? Result<Page<ProviderView>>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The provider projection is incomplete."))
            : Result<Page<ProviderView>>.Success(page);
    }
}
