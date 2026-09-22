using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Allows developer identity continuation only while developer authentication is enabled.</summary>
sealed class ContinueWithDeveloperIdentityAuthorizer(DeveloperUserRegistration developerRegistration)
    : IRequestAuthorizer<ContinueWithDeveloperIdentity>
{
    public ValueTask<Result> AuthorizeAsync(
        IRequestContext<ContinueWithDeveloperIdentity> context,
        CancellationToken ct) =>
        ValueTask.FromResult(developerRegistration.Enabled
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Developer identity is only available when developer authentication is enabled.")));
}
