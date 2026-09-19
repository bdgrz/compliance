using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

public sealed class ContinueWithOidcProviderHandler(UserIdentityContinuation continuation)
    : IRequestHandler<ContinueWithOidcProvider, AuthenticatedUserIdentity>
{
    public ValueTask<Result<AuthenticatedUserIdentity>> HandleAsync(
        IRequestContext<ContinueWithOidcProvider> context,
        CancellationToken ct)
    {
        var actor = context.Actor;
        var providerIdentity = actor.Identities.FirstOrDefault(identity =>
            identity.IsAuthenticated &&
            !string.Equals(identity.FindFirst("iss")?.Value, "bdgrz", StringComparison.Ordinal));
        var provider = providerIdentity?.FindFirst("iss")?.Value;
        var identifier = providerIdentity?.FindFirst("sub")?.Value;
        if (actor.Identity?.IsAuthenticated is not true ||
            string.IsNullOrWhiteSpace(provider) ||
            string.IsNullOrWhiteSpace(identifier))
        {
            return ValueTask.FromResult(Result<AuthenticatedUserIdentity>.Failure(new RequestError(
                RequestErrorKind.Unauthorized,
                "An authenticated OIDC issuer and subject are required.")));
        }

        var assertedEmail = providerIdentity?.FindFirst("email")?.Value;
        var emailAddress = EmailAddresses.TryNormalize(assertedEmail, out var normalizedEmail)
            ? normalizedEmail
            : null;
        return continuation.ContinueAsync(
            provider.Trim(),
            identifier.Trim(),
            emailAddress,
            // An existing session is not consent to link a second external identity.
            // Identity linking needs its own reviewed, authorized request.
            existingUserId: null,
            context,
            ct);
    }
}
