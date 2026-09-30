using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>Records one complete, attributed component revision; revision 1 creates it.</summary>
[Discriminator("bdgrz.inventory.component.revision_recorded", 1)]
public sealed record TechnologyComponentRevisionRecorded(Uuid TenantId, Uuid ComponentId,
    long Revision, TechnologyComponentContent Content, ActorReference Actor,
    DateTimeOffset ChangedAt) : DomainEvent;
