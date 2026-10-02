using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>Records one complete, attributed operational process revision; revision 1 creates it.</summary>
[Discriminator("bdgrz.inventory.operational_process.revision_recorded", 1)]
public sealed record OperationalProcessRevisionRecorded(Uuid TenantId, Uuid OperationalProcessId, long Revision,
    OperationalProcessContent Content, ActorReference Actor, DateTimeOffset ChangedAt) : DomainEvent;
