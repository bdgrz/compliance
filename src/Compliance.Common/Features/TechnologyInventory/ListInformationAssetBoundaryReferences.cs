using Bdgrz.Compliance.Features.Applications;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

[Discriminator("bdgrz.inventory.asset.boundary_references.list", 1)]
/// <summary>Lists current draft and current or historical approved boundary references, after source catchup.</summary>
public sealed record ListInformationAssetBoundaryReferences(Uuid TenantId, Uuid InformationAssetId,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<ApplicationBoundaryReferenceView>>, ITechnologyInventoryRequest, ICallable;
