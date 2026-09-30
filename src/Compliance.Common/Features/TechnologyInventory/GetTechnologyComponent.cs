using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

[Discriminator("bdgrz.inventory.component.get", 1)]
public sealed record GetTechnologyComponent(Uuid TenantId, Uuid ComponentId,
    long? MinimumRevision = null)
    : IRequest<TechnologyComponentView>, ITechnologyInventoryRequest, ICallable;
