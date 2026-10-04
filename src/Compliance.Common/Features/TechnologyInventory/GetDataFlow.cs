using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

[Discriminator("bdgrz.inventory.data_flow.get", 1)]
public sealed record GetDataFlow(Uuid TenantId, Uuid DataFlowId, long? MinimumRevision = null)
    : IRequest<DataFlowView>, ITechnologyInventoryReadRequest, ICallable;
