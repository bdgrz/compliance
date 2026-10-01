using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

[Discriminator("bdgrz.provider.revision.get", 1)]
public sealed record GetProviderRevision(Uuid TenantId, Uuid ProviderId, long Revision)
    : IRequest<ProviderView>, IProviderRequest, ICallable;
