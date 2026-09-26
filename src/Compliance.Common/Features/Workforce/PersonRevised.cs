using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

[Discriminator("bdgrz.workforce.person.revised", 1)]
public sealed record PersonRevised(Uuid TenantId, Uuid PersonId, long Revision,
    string DisplayName, string? WorkEmail, ActorReference Actor, DateTimeOffset ChangedAt)
    : DomainEvent;
