using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>Verifies the creator before a self-service organization is registered.</summary>
sealed class RegisterTenantAuthorizer(PlatformOperatorAuthority operators,
    IEmailAddressDirectoryReader emails) : IRequestAuthorizer<RegisterTenant>
{
    public async ValueTask<Result> AuthorizeAsync(IRequestContext<RegisterTenant> context,
        CancellationToken ct)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Organization creation requires a Bdgrz user identity."));

        // Local developer authentication retains legacy registration for existing fixtures.
        if (operators.DeveloperAuthentication)
            return Result.Success;

        if (!EmailAddresses.TryNormalize(context.Request.FirstAdministratorEmail, out var email))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Verify an email address before creating an organization."));
        var address = await emails.GetAsync(email, ct).ConfigureAwait(false);
        return address?.UserId == userId && address.Verified
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Verify an email address you own before creating an organization."));
    }
}
