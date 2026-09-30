using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>Records one complete, attributed data-flow version; revision 1 creates it.</summary>
[Discriminator("bdgrz.inventory.data_flow.revision_recorded", 1)]
public sealed record DataFlowRevisionRecorded(Uuid TenantId, Uuid DataFlowId, long Revision,
    DataFlowContent Content, ActorReference Actor, DateTimeOffset ChangedAt) : DomainEvent;
