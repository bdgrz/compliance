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

        if (!operators.DeveloperAuthentication)
        {
            if (string.IsNullOrWhiteSpace(context.Request.LegalName))
                return ValueTask.FromResult(Result<TenantRegistration>.Failure(new RequestError(
                    RequestErrorKind.Validation, "A legal name is required.")));
            if (context.Request.FirstAdministratorEmail is not null)
                return ValueTask.FromResult(Result<TenantRegistration>.Failure(new RequestError(
                    RequestErrorKind.Validation,
                    "First administrator invitations are not supported for self-service creation.")));
        }

        return executor.ExecuteAsync(
            new Tenant(context.RequestId),
            tenant => AggregateOutcome.CommitOnSuccess(
                tenant.Register(ownerUserId, context.Request.Name, context.Request.Slug,
                    context.Request.LegalName, context.Request.FirstAdministratorEmail,
                    creatorIsAdministrator: !operators.DeveloperAuthentication)),
            context, ct);
    }
}
