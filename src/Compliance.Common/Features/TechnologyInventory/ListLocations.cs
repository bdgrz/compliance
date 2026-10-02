using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

[Discriminator("bdgrz.inventory.location.list", 1)]
public sealed record ListLocations(Uuid TenantId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<LocationView>>, ITechnologyInventoryRequest, ICallable;
