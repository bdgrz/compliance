using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

[Discriminator("bdgrz.workforce.person.recorded", 1)]
public sealed record PersonRecorded(Uuid TenantId, Uuid PersonId, string DisplayName,
    string? WorkEmail, ActorReference Actor, DateTimeOffset ChangedAt) : DomainEvent;
