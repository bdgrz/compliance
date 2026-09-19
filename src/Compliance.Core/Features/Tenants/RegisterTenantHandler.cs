using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class RegisterTenantHandler(IAggregateExecutor executor, PlatformOperatorAuthority operators)
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

        if (!operators.DeveloperAuthentication &&
            (string.IsNullOrWhiteSpace(context.Request.LegalName) ||
             string.IsNullOrWhiteSpace(context.Request.FirstAdministratorEmail)))
            return ValueTask.FromResult(Result<TenantRegistration>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "Legal name and first administrator email are required for provisioning.")));

        return executor.ExecuteAsync(
            new Tenant(context.RequestId),
            tenant => AggregateOutcome.CommitOnSuccess(
                tenant.Register(ownerUserId, context.Request.Name, context.Request.Slug,
                    context.Request.LegalName, context.Request.FirstAdministratorEmail)),
            context, ct);
    }
}
