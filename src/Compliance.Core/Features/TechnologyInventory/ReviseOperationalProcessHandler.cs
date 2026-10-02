using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

public sealed class ReviseOperationalProcessHandler(IAggregateExecutor executor,
    TechnologyInventoryReferences references, TimeProvider clock)
    : IRequestHandler<ReviseOperationalProcess>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ReviseOperationalProcess> context,
        CancellationToken ct)
    {
        var actor = TechnologyInventoryActor.From(context);
        var request = context.Request;
        var owner = await references.ValidateOwnerAsync(request.TenantId,
            request.OwnerPersonId, ct).ConfigureAwait(false);
        if (owner is not null)
            return Result.Failure(owner);
        return await executor.ExecuteAsync(new OperationalProcessRegister(request.TenantId),
            register => CommandFailureRequestAdapter.ToOutcome(register.Revise(
                request.OperationalProcessId, request.ExpectedRevision, current => current with
                {
                    Name = request.Name,
                    Purpose = request.Purpose,
                    OwnerPersonId = request.OwnerPersonId,
                    Lifecycle = request.Lifecycle,
                    Inputs = request.Inputs,
                    Outputs = request.Outputs,
                }, actor, clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
