using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

public sealed class ContinueWithDeveloperIdentityHandler(
    UserIdentityContinuation continuation,
    DeveloperUserRegistration developerRegistration)
    : IRequestHandler<ContinueWithDeveloperIdentity, AuthenticatedUserIdentity>
{
    public ValueTask<Result<AuthenticatedUserIdentity>> HandleAsync(
        IRequestContext<ContinueWithDeveloperIdentity> context,
        CancellationToken ct)
    {
        if (!developerRegistration.Enabled)
        {
            return ValueTask.FromResult(Result<AuthenticatedUserIdentity>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "Developer identity is only available when developer authentication is enabled.")));
        }

        if (!EmailAddresses.TryNormalize(context.Request.EmailAddress, out var normalizedEmail))
        {
            return ValueTask.FromResult(Result<AuthenticatedUserIdentity>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "Enter a valid email address.")));
        }

        var identifier = Uuid.CreateVersion5(
            DeveloperUserRegistration.IdentifierNamespaceId,
            normalizedEmail).ToString();
        return continuation.ContinueAsync(
            DeveloperUserRegistration.Provider,
            identifier,
            normalizedEmail,
            UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId) ? userId : null,
            context,
            ct);
    }
}
