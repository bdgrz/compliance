using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

public sealed class ContinueWithOidcProviderHandler(UserIdentityContinuation continuation)
    : IRequestHandler<ContinueWithOidcProvider, AuthenticatedUserIdentity>
{
    public ValueTask<Result<AuthenticatedUserIdentity>> HandleAsync(
        IRequestContext<ContinueWithOidcProvider> context,
        CancellationToken ct)
    {
        if (!OidcProviderClaims.TryGet(context.Actor, out var provider,
                out var identifier, out var emailAddress))
        {
            return ValueTask.FromResult(Result<AuthenticatedUserIdentity>.Failure(new RequestError(
                RequestErrorKind.Unauthorized,
                "An authenticated OIDC issuer and subject are required.")));
        }

        return continuation.ContinueAsync(
            provider,
            identifier,
            emailAddress,
            // An existing session is not consent to link a second external identity.
            // Identity linking needs its own reviewed, authorized request.
            existingUserId: null,
            context,
            ct);
    }
}
