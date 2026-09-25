using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class ListApplicationRevisionsHandler(IApplicationDirectoryReader directory,
    ApplicationHistoryReadConsistency consistency)
    : IRequestHandler<ListApplicationRevisions, Page<ApplicationRevisionView>>
{
    public async ValueTask<Result<Page<ApplicationRevisionView>>> HandleAsync(
        IRequestContext<ListApplicationRevisions> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ApplicationRevisionView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The application revision list limit must be between 1 and 200."));
        var freshness = await consistency.EnsureAsync(request.TenantId,
                request.ApplicationId, request.MinimumApplicationRevision, ct)
            .ConfigureAwait(false);
        if (!freshness.IsSuccess)
            return Result<Page<ApplicationRevisionView>>.Failure(freshness.Error);
        Page<ApplicationRevisionView>? page;
        try
        {
            page = await directory.ListRevisionsAsync(request.TenantId,
                    request.ApplicationId, request.Limit ?? 50, request.Cursor, ct)
                .ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ApplicationRevisionView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The application revision cursor is invalid."));
        }
        if (page is null || page.Items.Any(item => item.TenantId != request.TenantId ||
                                                   item.ApplicationId != request.ApplicationId))
            return Result<Page<ApplicationRevisionView>>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The application revision projection is incomplete."));
        return Result<Page<ApplicationRevisionView>>.Success(page);
    }
}
