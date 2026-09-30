using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

[Discriminator("bdgrz.workforce.service-identity.revised", 1)]
public sealed record ServiceIdentityRevised(Uuid TenantId, Uuid ServiceIdentityId, long Revision,
    ServiceIdentityTerms Terms, ActorReference Actor, DateTimeOffset ChangedAt) : DomainEvent;
