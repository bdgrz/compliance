using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed class ListClientServicesHandler(IClientServiceDirectoryReader directory)
    : IRequestHandler<ListClientServices, Page<ClientServiceView>>
{
    public async ValueTask<Result<Page<ClientServiceView>>> HandleAsync(
        IRequestContext<ListClientServices> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ClientServiceView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The client service list limit must be between 1 and 200."));
        Page<ClientServiceView> page;
        try
        {
            page = await directory.ListAsync(request.TenantId, request.Limit ?? 50,
                request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ClientServiceView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The client service cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId)
            ? Result<Page<ClientServiceView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The services were not found."))
            : Result<Page<ClientServiceView>>.Success(page);
    }
}
