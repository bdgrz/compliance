using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class RegisterTenantHandler(IAggregateExecutor executor)
    : IRequestHandler<RegisterTenant, TenantRegistration>
{
    public ValueTask<Result<TenantRegistration>> HandleAsync(
        IRequestContext<RegisterTenant> context,
        CancellationToken ct)
    {
        // RegisterTenantAuthorizer guarantees this before the handler ever runs.
        var ownerUserId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException(
                "RegisterTenantAuthorizer must reject requests without a Bdgrz user identity.");

        return executor.ExecuteAsync(
            new Tenant(context.RequestId),
            tenant => AggregateOutcome.CommitOnSuccess(
                tenant.Register(ownerUserId, context.Request.Name, context.Request.Slug)),
            context, ct);
    }
}
