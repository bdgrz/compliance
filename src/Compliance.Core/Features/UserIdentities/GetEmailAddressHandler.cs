using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

public sealed class GetEmailAddressHandler(IEmailAddressDirectoryReader directory)
    : IRequestHandler<GetEmailAddress, EmailAddressView>
{
    public async ValueTask<Result<EmailAddressView>> HandleAsync(
        IRequestContext<GetEmailAddress> context, CancellationToken ct)
    {
        if (!EmailAddresses.TryNormalize(context.Request.EmailAddress, out var normalized))
            return Result<EmailAddressView>.Failure(new RequestError(RequestErrorKind.Validation,
                "Enter a valid email address."));

        var address = await directory.GetAsync(normalized, ct).ConfigureAwait(false);
        return address is not null && address.UserId == context.Request.UserId
            ? Result<EmailAddressView>.Success(address)
            : Result<EmailAddressView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The email address was not found."));
    }
}
