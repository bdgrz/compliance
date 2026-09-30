using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

[Discriminator("bdgrz.inventory.component.list", 1)]
public sealed record ListTechnologyComponents(Uuid TenantId, int? Limit = null,
    string? Cursor = null)
    : IRequest<Page<TechnologyComponentView>>, ITechnologyInventoryRequest, ICallable;
