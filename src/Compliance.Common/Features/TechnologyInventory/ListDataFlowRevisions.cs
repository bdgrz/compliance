using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

[Discriminator("bdgrz.inventory.data_flow.revision.list", 1)]
public sealed record ListDataFlowRevisions(Uuid TenantId, Uuid DataFlowId, int? Limit = null,
    string? Cursor = null, long? MinimumRevision = null)
    : IRequest<Page<DataFlowView>>, ITechnologyInventoryRequest, ICallable;
