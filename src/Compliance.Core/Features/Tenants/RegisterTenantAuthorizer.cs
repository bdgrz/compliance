using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>Tenant registration requires an actor carrying a Bdgrz user identity.</summary>
sealed class RegisterTenantAuthorizer : IRequestAuthorizer<RegisterTenant>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<RegisterTenant> context, CancellationToken ct) =>
        ValueTask.FromResult(UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _)
            ? Result.Success
            : Result.Failure(new RequestError(
                RequestErrorKind.Unauthorized,
                "Tenant registration requires a Bdgrz user identity.")));
}
