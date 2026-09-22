using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Confirms Control-reference reads use a stable, caught-up projection.</summary>
public sealed class ApplicationControlDraftReferenceReadConsistency(
    IApplicationControlDraftReferenceDirectory directory, IDomainEventReader events)
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
        if (checkpoint != fence)
            return BehindSource();
        return await HasPendingSourceAsync(tenantId, checkpoint, ct).ConfigureAwait(false)
            ? BehindSource()
            : Result.Success;
    }

    public async ValueTask<Result> EnsureCaughtUpAsync(Uuid tenantId, CancellationToken ct)
    {
        var capture = await CaptureAsync(tenantId, ct).ConfigureAwait(false);
        return capture.IsSuccess ? Result.Success : Result.Failure(capture.Error);
    }

    async ValueTask<bool> HasPendingSourceAsync(Uuid tenantId, ProjectionCheckpoint checkpoint,
        CancellationToken ct)
    {
        await using var pending = events.ReadAsync(
                EventStreamPattern.ForPattern(tenantId.ToString(), "controls"), checkpoint.Cursor, ct)
            .GetAsyncEnumerator(ct);
        return await pending.MoveNextAsync().ConfigureAwait(false);
    }

    static Result BehindSource() => Result.Failure(BehindSourceError());

    static RequestError BehindSourceError() => new(RequestErrorKind.Conflict,
        "The application control draft reference projection has not reached the source.",
        isTransient: true);
}
