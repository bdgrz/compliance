using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>Checks the source area cursor before returning even an empty list.</summary>
public sealed class ControlDraftListReadConsistency(IControlDraftDirectoryReader directory,
    IDomainEventReader events)
{
    public async ValueTask<Result> EnsureCaughtUpAsync(Uuid tenantId, CancellationToken ct)
    {
        var checkpoint = await directory.LoadCheckpointAsync(tenantId, ct)
            .ConfigureAwait(false);
        await using var pending = events.ReadAsync(
            EventStreamPattern.ForPattern(tenantId.ToString(), "controls"),
            checkpoint.Cursor, ct).GetAsyncEnumerator(ct);
        return await pending.MoveNextAsync().ConfigureAwait(false)
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The control draft list projection has not reached the source.",
                isTransient: true))
            : Result.Success;
    }
}
