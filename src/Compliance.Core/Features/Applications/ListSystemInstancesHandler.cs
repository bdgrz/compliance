using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class ListSystemInstancesHandler(IApplicationDirectoryReader directory,
    SystemInstanceReadConsistency consistency)
    : IRequestHandler<ListSystemInstances, Page<SystemInstanceView>>
{
    public async ValueTask<Result<Page<SystemInstanceView>>> HandleAsync(
        IRequestContext<ListSystemInstances> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<SystemInstanceView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The system instance list limit must be between 1 and 200."));
        var freshness = await consistency.EnsureAsync(request.TenantId, request.ApplicationId,
            request.MinimumApplicationRevision, null, null, ct).ConfigureAwait(false);
        if (!freshness.IsSuccess)
            return Result<Page<SystemInstanceView>>.Failure(freshness.Error);
        Page<SystemInstanceView> page;
        try
        {
            page = await directory.ListInstancesAsync(request.TenantId, request.ApplicationId,
                request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<SystemInstanceView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The system instance cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId ||
                                      item.ApplicationId != request.ApplicationId)
            ? Result<Page<SystemInstanceView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The system instances were not found."))
            : Result<Page<SystemInstanceView>>.Success(page);
    }
}
