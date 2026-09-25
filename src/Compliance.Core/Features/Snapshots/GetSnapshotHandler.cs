using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

public sealed class GetSnapshotHandler(ISnapshotDirectoryReader directory,
    IAggregateReader reader) : IRequestHandler<GetSnapshot, SnapshotView>
{
    public async ValueTask<Result<SnapshotView>> HandleAsync(
        IRequestContext<GetSnapshot> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.MinimumRevision is < 1)
            return Result<SnapshotView>.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum revision must be positive."));
        var view = await directory.GetAsync(request.TenantId, request.SnapshotId, ct)
            .ConfigureAwait(false);
        if (request.MinimumRevision is { } minimum &&
            (view is null || view.Revision < minimum))
        {
            var source = await reader.HydrateAsync(new ImmutableSnapshot(request.TenantId,
                request.SnapshotId), ct).ConfigureAwait(false);
            if (!source.IsFrozen)
                return Result<SnapshotView>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The snapshot was not found."));
            return Result<SnapshotView>.Failure(new RequestError(RequestErrorKind.Conflict,
                minimum > 1
                    ? "The snapshot source has not reached the requested revision."
                    : "The snapshot projection has not reached the requested revision."));
        }
        if (view is null || view.TenantId != request.TenantId ||
            view.SnapshotId != request.SnapshotId)
            return Result<SnapshotView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The snapshot was not found."));
        return SnapshotContentIdentity.MatchesView(view)
            ? Result<SnapshotView>.Success(view)
            : Result<SnapshotView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The stored snapshot manifest failed integrity verification."));
    }
}
