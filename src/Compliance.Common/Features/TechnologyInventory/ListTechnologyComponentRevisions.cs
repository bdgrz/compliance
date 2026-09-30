using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

[Discriminator("bdgrz.inventory.component.revision.list", 1)]
public sealed record ListTechnologyComponentRevisions(Uuid TenantId, Uuid ComponentId,
    int? Limit = null, string? Cursor = null, long? MinimumRevision = null)
    : IRequest<Page<TechnologyComponentView>>, ITechnologyInventoryRequest, ICallable;
