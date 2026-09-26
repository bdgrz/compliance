using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public sealed class ListPeopleHandler(IPersonDirectoryReader directory,
    PersonReadConsistency consistency) : IRequestHandler<ListPeople, Page<PersonView>>
{
    public async ValueTask<Result<Page<PersonView>>> HandleAsync(
        IRequestContext<ListPeople> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<PersonView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The person list limit must be between 1 and 200."));
        var ready = await consistency.EnsureListCaughtUpAsync(request.TenantId, ct)
            .ConfigureAwait(false);
        if (!ready.IsSuccess)
            return Result<Page<PersonView>>.Failure(ready.Error);
        Page<PersonView> page;
        try
        {
            page = await directory.ListAsync(request.TenantId, request.Limit ?? 50,
                request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<PersonView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The person cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId)
            ? Result<Page<PersonView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The people were not found."))
            : Result<Page<PersonView>>.Success(page);
    }
}
