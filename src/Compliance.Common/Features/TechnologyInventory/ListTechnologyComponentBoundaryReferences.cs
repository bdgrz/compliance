using Bdgrz.Compliance.Features.Applications;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

[Discriminator("bdgrz.inventory.component.boundary_references.list", 1)]
/// <summary>Lists current draft and current or historical approved boundary references, after source catchup.</summary>
public sealed record ListTechnologyComponentBoundaryReferences(Uuid TenantId, Uuid ComponentId,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<ApplicationBoundaryReferenceView>>, ITechnologyInventoryRequest, ICallable;
