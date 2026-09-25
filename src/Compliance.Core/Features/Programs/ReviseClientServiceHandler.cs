using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed class ReviseClientServiceHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<ReviseClientService>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviseClientService> context,
        CancellationToken ct)
    {
        var (memberId, display) = ClientServiceActor.Snapshot(context);
        return executor.ExecuteAsync(new ClientService(context.Request.TenantId,
                context.Request.ServiceId),
            service => AggregateOutcome.CommitOnSuccess(service.Revise(context.Request.ExpectedRevision,
                context.Request.Name, context.Request.Purpose, context.Request.OwnerReference,
                memberId, display, clock.GetUtcNow())), context, ct);
    }
}
