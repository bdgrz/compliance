using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

public sealed class CommitmentDraftListReadConsistency(ICommitmentDraftDirectoryReader directory,
    IDomainEventReader events)
{
    public async ValueTask<Result> EnsureCaughtUpAsync(Uuid tenantId, CancellationToken ct)
    {
        var checkpoint = await directory.LoadCheckpointAsync(tenantId, ct)
            .ConfigureAwait(false);
        await using var pending = events.ReadAsync(
            EventStreamPattern.ForPattern(tenantId.ToString(), "commitment-drafts"),
            checkpoint.Cursor, ct).GetAsyncEnumerator(ct);
        return await pending.MoveNextAsync().ConfigureAwait(false)
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The draft list projection has not reached the source.", isTransient: true))
            : Result.Success;
    }
}
