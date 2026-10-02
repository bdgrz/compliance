using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

[Discriminator("bdgrz.inventory.operational_process.get", 1)]
public sealed record GetOperationalProcess(Uuid TenantId, Uuid OperationalProcessId, long? MinimumRevision = null)
    : IRequest<OperationalProcessView>, ITechnologyInventoryRequest, ICallable;
