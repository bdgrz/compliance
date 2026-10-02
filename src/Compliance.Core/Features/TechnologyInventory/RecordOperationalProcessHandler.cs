using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

public sealed class RecordOperationalProcessHandler(IAggregateExecutor executor,
    TechnologyInventoryReferences references, TimeProvider clock)
    : IRequestHandler<RecordOperationalProcess, OperationalProcessRegistration>
{
    public async ValueTask<Result<OperationalProcessRegistration>> HandleAsync(
        IRequestContext<RecordOperationalProcess> context, CancellationToken ct)
    {
        var actor = TechnologyInventoryActor.From(context);
        var request = context.Request;
        var owner = await references.ValidateOwnerAsync(request.TenantId,
            request.OwnerPersonId, ct).ConfigureAwait(false);
        if (owner is not null)
            return Result<OperationalProcessRegistration>.Failure(owner);
        var content = new OperationalProcessContent(request.Name, request.Purpose,
            request.OwnerPersonId, request.Inputs, request.Outputs,
            TechnologyInventoryRules.Active);
        return await executor.ExecuteAsync(new OperationalProcessRegister(request.TenantId),
            register => AggregateOutcome.CommitOnSuccess(register.Record(context.RequestId,
                content, actor, clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
