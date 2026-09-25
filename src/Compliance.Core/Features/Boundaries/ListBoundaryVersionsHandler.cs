using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public sealed class ListBoundaryVersionsHandler(IBoundaryDirectoryReader directory,
    BoundaryHistoryReadConsistency consistency)
    : IRequestHandler<ListBoundaryVersions, Page<BoundaryVersionView>>
{
    public async ValueTask<Result<Page<BoundaryVersionView>>> HandleAsync(
        IRequestContext<ListBoundaryVersions> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<BoundaryVersionView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The boundary version list limit must be between 1 and 200."));
        if (request.MinimumBoundaryRevision is { } minimum)
        {
            var freshness = await consistency.EnsureAsync(request.TenantId, request.BoundaryId,
                minimum, ct).ConfigureAwait(false);
            if (!freshness.IsSuccess)
                return Result<Page<BoundaryVersionView>>.Failure(freshness.Error);
        }
        Page<BoundaryVersionView>? page;
        try
        {
            page = await directory.ListVersionsAsync(request.TenantId,
                request.BoundaryId, request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<BoundaryVersionView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The boundary version cursor is invalid."));
        }
        return page is null
            ? Result<Page<BoundaryVersionView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The boundary was not found."))
            : page.Items.Any(item => item.TenantId != request.TenantId ||
                                     item.BoundaryId != request.BoundaryId)
                ? Result<Page<BoundaryVersionView>>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The boundary version projection has an invalid scope."))
                : Result<Page<BoundaryVersionView>>.Success(page);
    }
}
