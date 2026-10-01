using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

[Discriminator("bdgrz.provider.get", 1)]
public sealed record GetProvider(Uuid TenantId, Uuid ProviderId, long? MinimumRevision = null)
    : IRequest<ProviderView>, IProviderRequest, ICallable;
