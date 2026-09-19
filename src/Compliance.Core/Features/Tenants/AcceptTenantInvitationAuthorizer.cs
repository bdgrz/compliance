using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

sealed class AcceptTenantInvitationAuthorizer(IEmailAddressDirectoryReader emails)
    : IRequestAuthorizer<AcceptTenantInvitation>
{
    public async ValueTask<Result> AuthorizeAsync(
        IRequestContext<AcceptTenantInvitation> context, CancellationToken ct)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Invitation acceptance requires a Bdgrz user identity."));
        if (!EmailAddresses.TryNormalize(context.Request.EmailAddress, out var normalized))
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "Enter a valid email address."));

        var address = await emails.GetAsync(normalized, ct).ConfigureAwait(false);
        return address?.UserId == userId && address.Verified
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Verify the invited email address before accepting."));
    }
}
