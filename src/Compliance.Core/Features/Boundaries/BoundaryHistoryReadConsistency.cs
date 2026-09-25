using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public sealed class BoundaryHistoryReadConsistency(IBoundaryDirectoryReader directory,
    IAggregateReader reader)
{
    public async ValueTask<Result> EnsureAsync(Uuid tenantId, Uuid boundaryId,
        long minimumRevision, CancellationToken ct)
    {
        if (minimumRevision < 1)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum boundary revision must be positive."));
        var view = await directory.GetAsync(tenantId, boundaryId, ct).ConfigureAwait(false);
        if (view is not null && view.Revision >= minimumRevision)
            return Result.Success;
        var source = await reader.HydrateAsync(new SystemBoundary(tenantId, boundaryId), ct)
            .ConfigureAwait(false);
        if (!source.IsCreated || !source.IsVisible)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The boundary was not found."));
        return Result.Failure(new RequestError(RequestErrorKind.Conflict,
            source.Revision < minimumRevision
                ? $"The boundary source has not reached revision {minimumRevision}."
                : $"The boundary projection has not reached revision {minimumRevision}."));
    }
}
