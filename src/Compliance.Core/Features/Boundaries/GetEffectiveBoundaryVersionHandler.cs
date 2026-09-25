using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public sealed class GetEffectiveBoundaryVersionHandler(IBoundaryDirectoryReader directory,
    BoundaryHistoryReadConsistency consistency)
    : IRequestHandler<GetEffectiveBoundaryVersion, BoundaryVersionView>
{
    public async ValueTask<Result<BoundaryVersionView>> HandleAsync(
        IRequestContext<GetEffectiveBoundaryVersion> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.MinimumBoundaryRevision is { } minimum)
        {
            var freshness = await consistency.EnsureAsync(request.TenantId, request.BoundaryId,
                minimum, ct).ConfigureAwait(false);
            if (!freshness.IsSuccess)
                return Result<BoundaryVersionView>.Failure(freshness.Error);
        }
        var version = await directory.GetEffectiveVersionAsync(request.TenantId,
            request.BoundaryId, request.EffectiveOn, ct).ConfigureAwait(false);
        return version is null
            ? Result<BoundaryVersionView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "No approved boundary version was effective on that date."))
            : Result<BoundaryVersionView>.Success(version);
    }
}
