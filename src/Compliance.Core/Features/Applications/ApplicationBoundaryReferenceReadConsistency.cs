using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
/// A reverse lookup has no source-side record index. Check the projection's exact area cursor
/// before returning even an empty page, so initial backfill and worker lag are explicit.
/// </summary>
public sealed class ApplicationBoundaryReferenceReadConsistency(
    IApplicationBoundaryReferenceDirectory directory, IDomainEventReader events)
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
        if (checkpoint != fence || await HasPendingSourceAsync(tenantId, checkpoint, ct)
                .ConfigureAwait(false))
            return Result.Failure(BehindSourceError());
        return Result.Success;
    }

    public async ValueTask<Result> EnsureCaughtUpAsync(Uuid tenantId, CancellationToken ct)
    {
        var capture = await CaptureAsync(tenantId, ct).ConfigureAwait(false);
        return capture.IsSuccess ? Result.Success : Result.Failure(capture.Error);
    }

    async ValueTask<bool> HasPendingSourceAsync(Uuid tenantId,
        ProjectionCheckpoint checkpoint, CancellationToken ct)
    {
        await using var pending = events.ReadAsync(
                EventStreamPattern.ForPattern(tenantId.ToString(), "boundaries"),
                checkpoint.Cursor, ct).GetAsyncEnumerator(ct);
        return await pending.MoveNextAsync().ConfigureAwait(false);
    }

    static RequestError BehindSourceError() => new(RequestErrorKind.Conflict,
        "The boundary reference projection has not reached the source.", isTransient: true);
}
