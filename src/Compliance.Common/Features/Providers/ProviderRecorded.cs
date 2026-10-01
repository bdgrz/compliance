using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

[Discriminator("bdgrz.provider.recorded", 1)]
public sealed record ProviderRecorded(Uuid TenantId, Uuid ProviderId, Uuid RequestId,
    ProviderContent Content, ActorReference Actor, DateTimeOffset RecordedAt) : DomainEvent;
