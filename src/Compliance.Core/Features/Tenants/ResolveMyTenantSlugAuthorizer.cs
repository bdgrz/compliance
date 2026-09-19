using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

sealed class ResolveMyTenantSlugAuthorizer : IRequestAuthorizer<ResolveMyTenantSlug>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<ResolveMyTenantSlug> context,
        CancellationToken ct) =>
        ValueTask.FromResult(UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Slug resolution requires a Bdgrz user identity.")));
}
