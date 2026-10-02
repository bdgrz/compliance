using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>Records one complete, attributed location revision; revision 1 creates it.</summary>
[Discriminator("bdgrz.inventory.location.revision_recorded", 1)]
public sealed record LocationRevisionRecorded(Uuid TenantId, Uuid LocationId, long Revision,
    LocationContent Content, ActorReference Actor, DateTimeOffset ChangedAt) : DomainEvent;
