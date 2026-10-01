using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

[Discriminator("bdgrz.providers.list", 1)]
public sealed record ListProviders(Uuid TenantId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<ProviderView>>, IProviderRequest, ICallable;
