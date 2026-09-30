using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>Records one complete, attributed asset revision; revision 1 creates it.</summary>
[Discriminator("bdgrz.inventory.information_asset.revision_recorded", 1)]
public sealed record InformationAssetRevisionRecorded(Uuid TenantId, Uuid InformationAssetId,
    long Revision, InformationAssetContent Content, ActorReference Actor,
    DateTimeOffset ChangedAt) : DomainEvent;
