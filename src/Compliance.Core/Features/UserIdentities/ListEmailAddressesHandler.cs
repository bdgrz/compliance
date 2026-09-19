using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

public sealed class ListEmailAddressesHandler(IEmailAddressDirectoryReader directory)
    : IRequestHandler<ListEmailAddresses, Page<EmailAddressView>>
{
    public async ValueTask<Result<Page<EmailAddressView>>> HandleAsync(
        IRequestContext<ListEmailAddresses> context, CancellationToken ct)
    {
        if (context.Request.Limit is < 1 or > 200)
            return Result<Page<EmailAddressView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "Limit must be between 1 and 200."));

        try
        {
            return Result<Page<EmailAddressView>>.Success(await directory.ListAsync(
                context.Request.UserId, context.Request.Limit, context.Request.Cursor, ct).ConfigureAwait(false));
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<EmailAddressView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The email address cursor is invalid."));
        }
    }
}
