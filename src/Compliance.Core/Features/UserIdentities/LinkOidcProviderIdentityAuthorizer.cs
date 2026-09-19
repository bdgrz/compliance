using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

sealed class LinkOidcProviderIdentityAuthorizer : IRequestAuthorizer<LinkOidcProviderIdentity>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<LinkOidcProviderIdentity> context,
        CancellationToken ct)
    {
        var allowed = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _) &&
            OidcProviderClaims.TryGet(context.Actor, out _, out _, out _);
        return ValueTask.FromResult(allowed
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Linking an OIDC identity requires the current browser session and provider proof.")));
    }
}
