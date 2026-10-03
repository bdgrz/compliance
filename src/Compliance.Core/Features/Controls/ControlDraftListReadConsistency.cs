using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>Checks the source area cursor before returning even an empty list.</summary>
public sealed class ControlDraftListReadConsistency(IControlDraftDirectoryReader directory,
    IDomainEventReader events)
{
    public async ValueTask<Result> EnsureCaughtUpAsync(Uuid tenantId, CancellationToken ct)
    {
        var capture = await CaptureAsync(tenantId, ct).ConfigureAwait(false);
        return capture.IsSuccess ? Result.Success : Result.Failure(capture.Error);
    }

    public async ValueTask<Result<ProjectionCheckpoint>> CaptureAsync(Uuid tenantId,
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
        if (checkpoint != fence ||
            await HasPendingSourceAsync(tenantId, checkpoint, ct).ConfigureAwait(false))
            return Result.Failure(BehindSourceError());
        return Result.Success;
    }

    async ValueTask<bool> HasPendingSourceAsync(Uuid tenantId,
        ProjectionCheckpoint checkpoint, CancellationToken ct)
    {
        await using var pending = events.ReadAsync(
                EventStreamPattern.ForPattern(tenantId.ToString(), "controls"), checkpoint.Cursor, ct)
            .GetAsyncEnumerator(ct);
        return await pending.MoveNextAsync().ConfigureAwait(false);
    }

    static RequestError BehindSourceError() => new(RequestErrorKind.Conflict,
        "The control draft list projection changed or has not reached the source. Retry the query.",
        isTransient: true);
}
