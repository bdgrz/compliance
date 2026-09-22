using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Requires an authenticated external provider identity before continuing an OIDC session.</summary>
sealed class ContinueWithOidcProviderAuthorizer : IRequestAuthorizer<ContinueWithOidcProvider>
{
    public ValueTask<Result> AuthorizeAsync(
        IRequestContext<ContinueWithOidcProvider> context,
        CancellationToken ct) =>
        ValueTask.FromResult(OidcProviderClaims.TryGet(context.Actor, out _, out _, out _)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "An authenticated OIDC issuer and subject are required.")));
}
