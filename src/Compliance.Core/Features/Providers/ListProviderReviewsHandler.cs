using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

public sealed class ListProviderReviewsHandler(IAssuranceReader directory, AssuranceReferences references,
    AssuranceReadConsistency consistency, AssuranceDisclosure disclosure)
    : IRequestHandler<ListProviderReviews, Page<ProviderReviewView>>
{
    public async ValueTask<Result<Page<ProviderReviewView>>> HandleAsync(
        IRequestContext<ListProviderReviews> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ProviderReviewView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The provider review list limit must be between 1 and 200."));
        var provider = await references.ProviderAsync(request.TenantId, request.ProviderId, ct).ConfigureAwait(false);
        if (!provider.IsSuccess)
            return Result<Page<ProviderReviewView>>.Failure(provider.Error);
        var freshness = await consistency.EnsureCaughtUpAsync(request.TenantId, ct).ConfigureAwait(false);
        if (!freshness.IsSuccess)
            return Result<Page<ProviderReviewView>>.Failure(freshness.Error);
        Page<ProviderReviewView> page;
        try
        {
            page = await directory.ListReviewsAsync(request.TenantId, request.ProviderId, request.Limit ?? 50,
                request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ProviderReviewView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The provider review cursor is invalid."));
        }
        if (page.Items.Any(item => item.TenantId != request.TenantId || item.ProviderId != request.ProviderId))
            return Result<Page<ProviderReviewView>>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The assurance projection is incomplete."));
        if (await disclosure.CanReadRestrictedAsync(context, ct).ConfigureAwait(false))
            return Result<Page<ProviderReviewView>>.Success(page);
        return Result<Page<ProviderReviewView>>.Success(page with
        {
            Items = page.Items.Select(static item => AssuranceRules.Redact(item)).ToArray(),
        });
    }
}
