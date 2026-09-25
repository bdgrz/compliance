using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public sealed class GetBoundaryVersionHandler(IBoundaryDirectoryReader directory,
    BoundaryHistoryReadConsistency consistency)
    : IRequestHandler<GetBoundaryVersion, BoundaryVersionView>
{
    public async ValueTask<Result<BoundaryVersionView>> HandleAsync(
        IRequestContext<GetBoundaryVersion> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.MinimumBoundaryRevision is { } minimum)
        {
            var freshness = await consistency.EnsureAsync(request.TenantId, request.BoundaryId,
                minimum, ct).ConfigureAwait(false);
            if (!freshness.IsSuccess)
                return Result<BoundaryVersionView>.Failure(freshness.Error);
        }
        var version = await directory.GetVersionAsync(request.TenantId, request.BoundaryId,
            request.VersionId, ct).ConfigureAwait(false);
        return version is null
            ? Result<BoundaryVersionView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The boundary version was not found."))
            : Result<BoundaryVersionView>.Success(version);
    }
}
