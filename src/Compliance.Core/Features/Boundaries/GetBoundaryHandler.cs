using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public sealed class GetBoundaryHandler(IBoundaryDirectoryReader directory,
    IAggregateReader reader)
    : IRequestHandler<GetBoundary, BoundaryView>
{
    public async ValueTask<Result<BoundaryView>> HandleAsync(IRequestContext<GetBoundary> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.MinimumRevision is < 1)
            return Result<BoundaryView>.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum revision must be positive."));
        var view = await directory.GetAsync(request.TenantId,
            request.BoundaryId, ct).ConfigureAwait(false);
        if (request.MinimumRevision is { } minimum &&
            (view is null || view.Revision < minimum))
        {
            var current = await reader.HydrateAsync(new SystemBoundary(request.TenantId,
                request.BoundaryId), ct).ConfigureAwait(false);
            if (!current.IsCreated || !current.IsVisible)
                return Result<BoundaryView>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The boundary was not found."));
            return Result<BoundaryView>.Failure(new RequestError(RequestErrorKind.Conflict,
                current.Revision < minimum
                    ? $"The boundary source has not reached revision {minimum}."
                    : $"The boundary projection has not reached revision {minimum}."));
        }
        return view is null
            ? Result<BoundaryView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The boundary was not found."))
            : Result<BoundaryView>.Success(view);
    }
}
