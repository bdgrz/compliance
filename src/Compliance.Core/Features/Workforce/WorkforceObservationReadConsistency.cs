using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public sealed class WorkforceObservationReadConsistency(IWorkforceObservationDirectoryReader directory,
    IDomainEventReader events)
{
    /// <summary>Checks the roster source cursor before returning observations or owner state.</summary>
    public async ValueTask<Result> EnsureCaughtUpAsync(Uuid tenantId, CancellationToken ct)
    {
        var checkpoint = await directory.LoadCheckpointAsync(tenantId, ct).ConfigureAwait(false);
        await using var pending = events.ReadAsync(
            EventStreamPattern.ForPattern(tenantId.ToString(), "work-relationships"),
            checkpoint.Cursor, ct).GetAsyncEnumerator(ct);
        return await pending.MoveNextAsync().ConfigureAwait(false)
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The workforce observation projection has not reached the roster.",
                isTransient: true))
            : Result.Success;
    }
}
