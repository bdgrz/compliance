using Bdgrz.Compliance.Features.Boundaries;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

/// <summary>
/// Confirms the two tenant-scoped projections that supply setup work have reached their source
/// before exposing a derived result.
/// </summary>
public sealed class ProgramSetupWorkReadConsistency(IProgramDirectoryReader programs,
    IBoundaryDirectoryReader boundaries, IDomainEventReader events)
{
    public async ValueTask<Result<ProgramSetupWorkReadFence>> CaptureAsync(Uuid tenantId,
        CancellationToken ct)
    {
        var pattern = EventStreamPattern.ForPattern(tenantId.ToString());
        var programCheckpoint = await programs.LoadCheckpointAsync(tenantId, ct)
            .ConfigureAwait(false);
        var boundaryCheckpoint = await boundaries.LoadCheckpointAsync(tenantId, ct)
            .ConfigureAwait(false);
        var programPending = await HasPendingSourceAsync(pattern, programCheckpoint, ct)
            .ConfigureAwait(false);
        var boundaryPending = await HasPendingSourceAsync(pattern, boundaryCheckpoint, ct)
            .ConfigureAwait(false);
        return programPending || boundaryPending
            ? Result<ProgramSetupWorkReadFence>.Failure(BehindSourceError())
            : Result<ProgramSetupWorkReadFence>.Success(new ProgramSetupWorkReadFence(
                programCheckpoint, boundaryCheckpoint));
    }

    public async ValueTask<Result> ConfirmUnchangedAndCaughtUpAsync(Uuid tenantId,
        ProgramSetupWorkReadFence fence, CancellationToken ct)
    {
        var pattern = EventStreamPattern.ForPattern(tenantId.ToString());
        var programCheckpoint = await programs.LoadCheckpointAsync(tenantId, ct)
            .ConfigureAwait(false);
        var boundaryCheckpoint = await boundaries.LoadCheckpointAsync(tenantId, ct)
            .ConfigureAwait(false);
        if (programCheckpoint != fence.Program || boundaryCheckpoint != fence.Boundary)
            return BehindSource();

        var programPending = await HasPendingSourceAsync(pattern, programCheckpoint, ct)
            .ConfigureAwait(false);
        var boundaryPending = await HasPendingSourceAsync(pattern, boundaryCheckpoint, ct)
            .ConfigureAwait(false);
        return programPending || boundaryPending ? BehindSource() : Result.Success;
    }

    async ValueTask<bool> HasPendingSourceAsync(EventStreamPattern pattern,
        ProjectionCheckpoint checkpoint, CancellationToken ct)
    {
        await using var pending = events.ReadAsync(pattern, checkpoint.Cursor, ct)
            .GetAsyncEnumerator(ct);
        return await pending.MoveNextAsync().ConfigureAwait(false);
    }

    static Result BehindSource() => Result.Failure(BehindSourceError());

    static RequestError BehindSourceError() => new(RequestErrorKind.Conflict,
        "The setup work projections have not reached the source. Retry the query.",
        isTransient: true);
}
