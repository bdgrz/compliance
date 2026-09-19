using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class ReactivateTenantHandler(IAggregateExecutor executor) : IRequestHandler<ReactivateTenant>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReactivateTenant> context, CancellationToken ct)
    {
        var operatorUserId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId)
            ? userId
            : throw new InvalidOperationException("PlatformOperatorAuthorizer must reject this actor.");
        return executor.ExecuteAsync(new Tenant(context.Request.TenantId),
            tenant => AggregateOutcome.CommitOnSuccess(tenant.Reactivate(operatorUserId)), context, ct);
    }
}
