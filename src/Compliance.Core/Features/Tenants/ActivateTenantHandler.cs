using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class ActivateTenantHandler(IAggregateExecutor executor) : IRequestHandler<ActivateTenant>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ActivateTenant> context, CancellationToken ct) =>
        executor.ExecuteAsync(new Tenant(context.Request.TenantId),
            tenant => AggregateOutcome.CommitOnSuccess(tenant.Activate(context.Request.FirstAdministratorUserId,
                context.Request.FirstAdministratorEmail)),
            context, ct);
}
