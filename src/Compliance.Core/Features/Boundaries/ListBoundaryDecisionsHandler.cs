using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public sealed class ListBoundaryDecisionsHandler(IBoundaryDirectoryReader directory)
    : IRequestHandler<ListBoundaryDecisions, Page<BoundaryDecisionView>>
{
    public async ValueTask<Result<Page<BoundaryDecisionView>>> HandleAsync(
        IRequestContext<ListBoundaryDecisions> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<BoundaryDecisionView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The boundary decision list limit must be between 1 and 200."));
        Page<BoundaryDecisionView>? page;
        try
        {
            page = await directory.ListDecisionsAsync(request.TenantId,
                request.BoundaryId, request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<BoundaryDecisionView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The boundary decision cursor is invalid."));
        }
        return page is null
            ? Result<Page<BoundaryDecisionView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The boundary was not found."))
            : page.Items.Any(item => item.TenantId != request.TenantId ||
                                     item.BoundaryId != request.BoundaryId)
                ? Result<Page<BoundaryDecisionView>>.Failure(new RequestError(
                    RequestErrorKind.Conflict, "The boundary decision projection has an invalid scope."))
                : Result<Page<BoundaryDecisionView>>.Success(page);
    }
}
