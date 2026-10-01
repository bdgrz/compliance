using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

[Discriminator("bdgrz.work.digest_preference.changed", 1)]
public sealed record WorkDigestPreferenceChanged(Uuid TenantId, Uuid MemberId,
    bool EmailDigestEnabled, ActorReference ChangedBy, DateTimeOffset ChangedAt) : DomainEvent;
