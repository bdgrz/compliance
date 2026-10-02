using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

[Discriminator("bdgrz.inventory.location.get", 1)]
public sealed record GetLocation(Uuid TenantId, Uuid LocationId, long? MinimumRevision = null)
    : IRequest<LocationView>, ITechnologyInventoryRequest, ICallable;
