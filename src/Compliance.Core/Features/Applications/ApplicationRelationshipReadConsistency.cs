using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class ApplicationRelationshipReadConsistency(
    IApplicationRelationshipDirectory directory, IDomainEventReader events)
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

    public async ValueTask<Result> EnsureCaughtUpAsync(Uuid tenantId,
        CancellationToken ct) =>
        await CaptureAsync(tenantId, ct).ConfigureAwait(false) is { IsSuccess: true }
            ? Result.Success
            : Result.Failure(BehindSourceError());

    async ValueTask<bool> HasPendingSourceAsync(Uuid tenantId,
        ProjectionCheckpoint checkpoint, CancellationToken ct)
    {
        await using var pending = events.ReadAsync(
                EventStreamPattern.ForPattern(tenantId.ToString(), "applications"),
                checkpoint.Cursor, ct).GetAsyncEnumerator(ct);
        return await pending.MoveNextAsync().ConfigureAwait(false);
    }

    static RequestError BehindSourceError() => new(RequestErrorKind.Conflict,
        "The application relationship projection has not reached the source.",
        isTransient: true);
}
