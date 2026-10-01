using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

[Discriminator("bdgrz.provider.revisions.list", 1)]
public sealed record ListProviderRevisions(Uuid TenantId, Uuid ProviderId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<ProviderView>>, IProviderRequest, ICallable;
