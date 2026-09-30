using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

[Discriminator("bdgrz.workforce.service-identity.recorded", 1)]
public sealed record ServiceIdentityRecorded(Uuid TenantId, Uuid ServiceIdentityId,
    ServiceIdentityTerms Terms, ActorReference Actor, DateTimeOffset ChangedAt) : DomainEvent;
