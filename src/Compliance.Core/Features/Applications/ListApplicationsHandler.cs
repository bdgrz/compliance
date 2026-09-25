using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class ListApplicationsHandler(IApplicationDirectoryReader directory)
    : IRequestHandler<ListApplications, Page<ApplicationView>>
{
    public async ValueTask<Result<Page<ApplicationView>>> HandleAsync(
        IRequestContext<ListApplications> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ApplicationView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The application list limit must be between 1 and 200."));
        Page<ApplicationView> page;
        try
        {
            page = await directory.ListAsync(request.TenantId, request.Limit ?? 50,
                request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ApplicationView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The application cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId)
            ? Result<Page<ApplicationView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The applications were not found."))
            : Result<Page<ApplicationView>>.Success(page);
    }
}
