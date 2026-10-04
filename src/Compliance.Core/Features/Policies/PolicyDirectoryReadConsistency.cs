using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

/// <summary>Confirms policy-directory reads use a stable, caught-up projection.</summary>
public sealed class PolicyDirectoryReadConsistency(IPolicyDirectoryReader directory,
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
        if (checkpoint != fence ||
            await HasPendingSourceAsync(tenantId, checkpoint, ct).ConfigureAwait(false))
            return Result.Failure(BehindSourceError());
        return Result.Success;
    }

    async ValueTask<bool> HasPendingSourceAsync(Uuid tenantId, ProjectionCheckpoint checkpoint,
        CancellationToken ct)
    {
        await using var pending = events.ReadAsync(EventStreamPattern.ForPattern(
                tenantId.ToString(), "policies"), checkpoint.Cursor, ct)
            .GetAsyncEnumerator(ct);
        return await pending.MoveNextAsync().ConfigureAwait(false);
    }

    static RequestError BehindSourceError() => new(RequestErrorKind.Conflict,
        "The policy list projection changed or has not reached the source. Retry the query.",
        isTransient: true);
}
