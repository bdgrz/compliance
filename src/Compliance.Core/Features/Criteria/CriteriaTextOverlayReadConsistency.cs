using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria;

sealed class CriteriaTextOverlayReadConsistency(ICriteriaTextOverlayDirectoryReader directory,
    IDomainEventReader events)
{
    public async ValueTask<Result<ProjectionCheckpoint>> CaptureAsync(Uuid tenantId,
        CancellationToken ct)
    {
        var checkpoint = await directory.LoadCheckpointAsync(tenantId, ct).ConfigureAwait(false);
        return await HasPendingSourceAsync(tenantId, checkpoint, ct).ConfigureAwait(false)
            ? Result<ProjectionCheckpoint>.Failure(BehindSourceError())
            : Result<ProjectionCheckpoint>.Success(checkpoint);
    }

    public async ValueTask<Result> ConfirmUnchangedAndCaughtUpAsync(Uuid tenantId,
        ProjectionCheckpoint fence, CancellationToken ct)
    {
        var checkpoint = await directory.LoadCheckpointAsync(tenantId, ct).ConfigureAwait(false);
        return checkpoint != fence ||
               await HasPendingSourceAsync(tenantId, checkpoint, ct).ConfigureAwait(false)
            ? Result.Failure(BehindSourceError())
            : Result.Success;
    }

    async ValueTask<bool> HasPendingSourceAsync(Uuid tenantId,
        ProjectionCheckpoint checkpoint, CancellationToken ct)
    {
        await using var pending = events.ReadAsync(EventStreamPattern.ForPattern(
                tenantId.ToString(), CriteriaTextOverlayLedger.Area), checkpoint.Cursor, ct)
            .GetAsyncEnumerator(ct);
        return await pending.MoveNextAsync().ConfigureAwait(false);
    }

    static RequestError BehindSourceError() => new(RequestErrorKind.Conflict,
        "The criteria text overlay projection changed or has not reached the source. Retry the query.",
        isTransient: true);
}
