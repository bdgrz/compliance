using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class OffboardTenantHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<OffboardTenant>
{
    public ValueTask<Result> HandleAsync(IRequestContext<OffboardTenant> context, CancellationToken ct)
    {
        var operatorUserId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId)
            ? userId
            : throw new InvalidOperationException("PlatformOperatorAuthorizer must reject this actor.");
        return executor.ExecuteAsync(new Tenant(context.Request.TenantId),
            tenant => AggregateOutcome.CommitOnSuccess(
                tenant.StartOffboarding(operatorUserId, context.Request.Reason, clock.GetUtcNow())), context, ct);
    }
}
