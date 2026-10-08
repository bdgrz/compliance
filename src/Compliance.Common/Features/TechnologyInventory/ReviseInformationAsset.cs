using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

[Discriminator("bdgrz.inventory.information_asset.revise", 1)]
public sealed record ReviseInformationAsset(Uuid TenantId, Uuid InformationAssetId,
    long ExpectedRevision, string Name, string Classification, string RetentionReference,
    Uuid OwnerPersonId, string Lifecycle, string? Description = null)
    : IRequest, ITechnologyInventoryWriteRequest, ICallable;
