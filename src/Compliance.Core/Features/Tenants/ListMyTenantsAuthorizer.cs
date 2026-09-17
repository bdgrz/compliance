using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>Listing the caller's own tenants requires an actor carrying a Bdgrz user identity.</summary>
sealed class ListMyTenantsAuthorizer : IRequestAuthorizer<ListMyTenants>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<ListMyTenants> context, CancellationToken ct) =>
        ValueTask.FromResult(UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _)
            ? Result.Success
            : Result.Failure(new RequestError(
                RequestErrorKind.Unauthorized,
                "Listing tenant memberships requires a Bdgrz user identity.")));
}
