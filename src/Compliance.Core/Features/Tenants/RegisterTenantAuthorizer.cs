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

        string? cursor = null;
        do
        {
            var page = await emails.ListAsync(userId, 50, cursor, ct).ConfigureAwait(false);
            if (page.Items.Any(address => address.UserId == userId && address.Verified))
                return Result.Success;
            cursor = page.NextCursor;
        } while (cursor is not null);

        return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
            "Verify an email address you own before creating an organization."));
    }
}
