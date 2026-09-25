using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed class ClientServiceHistoryReadConsistency(IClientServiceDirectoryReader directory,
    IAggregateReader reader)
{
    public async ValueTask<Result> EnsureAsync(Uuid tenantId, Uuid serviceId,
        long minimumRevision, CancellationToken ct)
    {
        if (minimumRevision < 1)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum service revision must be positive."));
        var view = await directory.GetAsync(tenantId, serviceId, ct).ConfigureAwait(false);
        if (view is not null && view.Revision >= minimumRevision)
            return Result.Success;
        var source = await reader.HydrateAsync(new ClientService(tenantId, serviceId), ct)
            .ConfigureAwait(false);
        if (!source.IsCreated)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The service was not found."));
        return Result.Failure(new RequestError(RequestErrorKind.Conflict,
            source.Revision < minimumRevision
                ? $"The service source has not reached revision {minimumRevision}."
                : $"The service projection has not reached revision {minimumRevision}."));
    }
}
