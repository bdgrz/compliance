using Bdgrz.Compliance.Features.Boundaries;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Uses BoundaryDirectory's full-tenant checkpoint to fence a complete queue read.</summary>
public sealed class WorkQueueReadConsistency(IBoundaryDirectoryReader tenantCheckpoint,
    IDomainEventReader events)
{
    public async ValueTask<Result<ProjectionCheckpoint>> CaptureAsync(Uuid tenantId,
        CancellationToken ct)
    {
        var checkpoint = await tenantCheckpoint.LoadCheckpointAsync(tenantId, ct)
            .ConfigureAwait(false);
        return await HasPendingSourceAsync(tenantId, checkpoint, ct).ConfigureAwait(false)
            ? Result<ProjectionCheckpoint>.Failure(BehindSourceError())
            : Result<ProjectionCheckpoint>.Success(checkpoint);
    }

    public async ValueTask<Result> ConfirmUnchangedAndCaughtUpAsync(Uuid tenantId,
        ProjectionCheckpoint fence, CancellationToken ct)
    {
        var checkpoint = await tenantCheckpoint.LoadCheckpointAsync(tenantId, ct)
            .ConfigureAwait(false);
        if (checkpoint != fence ||
            await HasPendingSourceAsync(tenantId, checkpoint, ct).ConfigureAwait(false))
            return Result.Failure(BehindSourceError());
        return Result.Success;
    }

    async ValueTask<bool> HasPendingSourceAsync(Uuid tenantId,
        ProjectionCheckpoint checkpoint, CancellationToken ct)
    {
        await using var pending = events.ReadAsync(
                EventStreamPattern.ForPattern(tenantId.ToString()), checkpoint.Cursor, ct)
            .GetAsyncEnumerator(ct);
        return await pending.MoveNextAsync().ConfigureAwait(false);
    }

    static RequestError BehindSourceError() => new(RequestErrorKind.Conflict,
        "The work queue sources changed or have not reached the tenant checkpoint. Retry the query.",
        isTransient: true);
}
