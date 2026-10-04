using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

[Discriminator("bdgrz.inventory.information_asset.revision.list", 1)]
public sealed record ListInformationAssetRevisions(Uuid TenantId, Uuid InformationAssetId,
    int? Limit = null, string? Cursor = null, long? MinimumRevision = null)
    : IRequest<Page<InformationAssetView>>, ITechnologyInventoryReadRequest, ICallable;
