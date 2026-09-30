using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

[Discriminator("bdgrz.inventory.information_asset.list", 1)]
public sealed record ListInformationAssets(Uuid TenantId, int? Limit = null,
    string? Cursor = null)
    : IRequest<Page<InformationAssetView>>, ITechnologyInventoryRequest, ICallable;
