using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

[Discriminator("bdgrz.inventory.data_flow.list", 1)]
public sealed record ListDataFlows(Uuid TenantId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<DataFlowView>>, ITechnologyInventoryReadRequest, ICallable;
