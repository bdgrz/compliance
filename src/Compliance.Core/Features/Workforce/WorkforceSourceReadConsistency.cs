using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public sealed class WorkforceSourceReadConsistency(IWorkforceSourceDirectoryReader directory,
    IAggregateReader reader, IDomainEventReader events)
{
    public async ValueTask<Result<WorkforceSourceView>> GetAsync(Uuid tenantId, Uuid observationId,
        long? minimumRevision, CancellationToken ct)
    {
        if (minimumRevision is < 1)
            return Result<WorkforceSourceView>.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum source observation revision must be positive."));
        var source = await reader.HydrateAsync(new WorkforceSourceObservation(tenantId, observationId), ct)
            .ConfigureAwait(false);
        if (!source.IsRecorded)
            return Result<WorkforceSourceView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The source observation was not found."));
        var view = await directory.GetAsync(tenantId, observationId, ct).ConfigureAwait(false);
        return view is not null && view.TenantId == tenantId && view.ObservationId == observationId &&
               view.Revision >= source.Revision && (minimumRevision is null || view.Revision >= minimumRevision)
            ? Result<WorkforceSourceView>.Success(view)
            : Result<WorkforceSourceView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The source observation projection has not reached the requested revision.", isTransient: true));
    }

    public async ValueTask<Result> EnsureListCaughtUpAsync(Uuid tenantId, CancellationToken ct)
    {
        var checkpoint = await directory.LoadCheckpointAsync(tenantId, ct).ConfigureAwait(false);
        await using var pending = events.ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString(),
            "workforce-source-observations"), checkpoint.Cursor, ct).GetAsyncEnumerator(ct);
        return await pending.MoveNextAsync().ConfigureAwait(false)
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The workforce source list projection has not reached the source.", isTransient: true))
            : Result.Success;
    }
}
