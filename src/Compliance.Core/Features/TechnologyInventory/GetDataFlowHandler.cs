using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

public sealed class GetDataFlowHandler(TechnologyInventoryReadConsistency consistency)
    : IRequestHandler<GetDataFlow, DataFlowView>
{
    public ValueTask<Result<DataFlowView>> HandleAsync(IRequestContext<GetDataFlow> context,
        CancellationToken ct) =>
        consistency.GetFlowAsync(context.Request.TenantId, context.Request.DataFlowId,
            context.Request.MinimumRevision, ct);
}
