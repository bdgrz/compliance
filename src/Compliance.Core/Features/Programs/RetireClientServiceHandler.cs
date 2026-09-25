using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed class RetireClientServiceHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<RetireClientService>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RetireClientService> context,
        CancellationToken ct)
    {
        var (memberId, display) = ClientServiceActor.Snapshot(context);
        return executor.ExecuteAsync(new ClientService(context.Request.TenantId,
                context.Request.ServiceId),
            service => AggregateOutcome.CommitOnSuccess(service.Retire(context.Request.ExpectedRevision,
                context.Request.Rationale, memberId, display, clock.GetUtcNow())), context, ct);
    }
}
