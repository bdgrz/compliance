using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

[Discriminator("bdgrz.inventory.information_asset.get", 1)]
public sealed record GetInformationAsset(Uuid TenantId, Uuid InformationAssetId,
    long? MinimumRevision = null)
    : IRequest<InformationAssetView>, ITechnologyInventoryRequest, ICallable;
