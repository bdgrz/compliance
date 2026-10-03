using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

public sealed class CommitmentDraftListReadConsistency(ICommitmentDraftDirectoryReader directory,
    IDomainEventReader events)
{
    public async ValueTask<Result> EnsureCaughtUpAsync(Uuid tenantId, CancellationToken ct)
    {
        var fence = await CaptureFenceAsync(tenantId, ct).ConfigureAwait(false);
        return fence.IsSuccess ? Result.Success : Result.Failure(fence.Error);
    }

    public async ValueTask<Result<ProjectionCheckpoint>> CaptureFenceAsync(Uuid tenantId,
        CancellationToken ct)
    {
        var checkpoint = await directory.LoadCheckpointAsync(tenantId, ct)
            .ConfigureAwait(false);
        return await HasPendingSourceAsync(tenantId, checkpoint, ct).ConfigureAwait(false)
            ? Result<ProjectionCheckpoint>.Failure(BehindSourceError())
            : Result<ProjectionCheckpoint>.Success(checkpoint);
    }

    public async ValueTask<Result> ConfirmUnchangedAndCaughtUpAsync(Uuid tenantId,
        ProjectionCheckpoint fence, CancellationToken ct)
    {
        var checkpoint = await directory.LoadCheckpointAsync(tenantId, ct)
            .ConfigureAwait(false);
        return checkpoint != fence ||
               await HasPendingSourceAsync(tenantId, checkpoint, ct).ConfigureAwait(false)
            ? Result.Failure(BehindSourceError())
            : Result.Success;
    }

    async ValueTask<bool> HasPendingSourceAsync(Uuid tenantId,
        ProjectionCheckpoint checkpoint, CancellationToken ct)
    {
        await using var pending = events.ReadAsync(
            EventStreamPattern.ForPattern(tenantId.ToString(), "commitment-drafts"),
            checkpoint.Cursor, ct).GetAsyncEnumerator(ct);
        return await pending.MoveNextAsync().ConfigureAwait(false);
    }

    static RequestError BehindSourceError() => new(RequestErrorKind.Conflict,
        "The commitment projection changed or has not reached the source. Retry the query.",
        isTransient: true);
}
