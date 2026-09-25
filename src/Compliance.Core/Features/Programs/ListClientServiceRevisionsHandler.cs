using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed class ListClientServiceRevisionsHandler(IClientServiceDirectoryReader directory,
    ClientServiceHistoryReadConsistency consistency)
    : IRequestHandler<ListClientServiceRevisions, Page<ClientServiceRevisionView>>
{
    public async ValueTask<Result<Page<ClientServiceRevisionView>>> HandleAsync(
        IRequestContext<ListClientServiceRevisions> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ClientServiceRevisionView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The client service revision list limit must be between 1 and 200."));
        if (request.MinimumServiceRevision is { } minimum)
        {
            var freshness = await consistency.EnsureAsync(request.TenantId, request.ServiceId,
                minimum, ct).ConfigureAwait(false);
            if (!freshness.IsSuccess)
                return Result<Page<ClientServiceRevisionView>>.Failure(freshness.Error);
        }
        Page<ClientServiceRevisionView>? page;
        try
        {
            page = await directory.ListRevisionsAsync(request.TenantId, request.ServiceId,
                request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ClientServiceRevisionView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The client service revision cursor is invalid."));
        }
        return page is null
            ? Result<Page<ClientServiceRevisionView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The service was not found."))
            : page.Items.Any(item => item.ServiceId != request.ServiceId)
                ? Result<Page<ClientServiceRevisionView>>.Failure(new RequestError(
                    RequestErrorKind.Conflict, "The service revision projection is incomplete."))
                : Result<Page<ClientServiceRevisionView>>.Success(page);
    }
}
