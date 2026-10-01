using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

[Discriminator("bdgrz.provider.revised", 1)]
public sealed record ProviderRevised(Uuid TenantId, Uuid ProviderId, Uuid RequestId,
    long Revision, ProviderContent Content, ActorReference Actor, DateTimeOffset RecordedAt) : DomainEvent;
