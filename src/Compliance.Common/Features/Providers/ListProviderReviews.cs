using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

[Discriminator("bdgrz.provider.reviews.list", 1)]
public sealed record ListProviderReviews(Uuid TenantId, Uuid ProviderId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<ProviderReviewView>>, IProviderRequest, ICallable;
