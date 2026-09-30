using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>Records a governed information asset in the tenant's manual inventory.</summary>
[Discriminator("bdgrz.inventory.information_asset.record", 1)]
public sealed record RecordInformationAsset(Uuid TenantId, string Name, string Classification,
    string RetentionReference, Uuid OwnerPersonId, string? Description = null)
    : IRequest<InformationAssetRegistration>, ITechnologyInventoryRequest, ICallable;
