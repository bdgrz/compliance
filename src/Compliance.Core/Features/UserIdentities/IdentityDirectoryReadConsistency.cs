using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

public sealed class IdentityDirectoryReadConsistency(
    IUserIdentityDirectoryReader directory,
    IDomainEventReader events) : IUserIdentityDirectoryReadConsistency
{
    static readonly EventStreamPattern Source =
        EventStreamPattern.ForPattern("bdgrz", "user-identities");

    public async ValueTask<Result> EnsureCaughtUpAsync(CancellationToken ct)
    {
        var checkpoint = await directory.LoadCheckpointAsync(ct).ConfigureAwait(false);
        await using var pending = events.ReadAsync(Source, checkpoint.Cursor, ct)
            .GetAsyncEnumerator(ct);
        return await pending.MoveNextAsync().ConfigureAwait(false)
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The identity directory has not reached the source. Retry the query.",
                isTransient: true))
            : Result.Success;
    }
}
