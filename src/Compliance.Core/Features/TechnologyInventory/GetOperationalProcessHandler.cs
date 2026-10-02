using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

public sealed class GetOperationalProcessHandler(InventoryRegisterReadConsistency consistency)
    : IRequestHandler<GetOperationalProcess, OperationalProcessView>
{
    public ValueTask<Result<OperationalProcessView>> HandleAsync(IRequestContext<GetOperationalProcess> context,
        CancellationToken ct) =>
        consistency.GetOperationalProcessAsync(context.Request.TenantId, context.Request.OperationalProcessId,
            context.Request.MinimumRevision, ct);
}
