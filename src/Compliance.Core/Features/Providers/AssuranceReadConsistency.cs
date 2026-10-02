using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

public sealed class AssuranceReadConsistency(IAssuranceReader directory, IDomainEventReader events)
{
    public async ValueTask<Result> EnsureCaughtUpAsync(Uuid tenantId, CancellationToken ct)
    {
        var checkpoint = await directory.LoadCheckpointAsync(tenantId, ct).ConfigureAwait(false);
        await using var pending = events.ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString(),
            ProviderAssuranceRegister.Area), checkpoint.Cursor, ct).GetAsyncEnumerator(ct);
        return await pending.MoveNextAsync().ConfigureAwait(false)
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The assurance projection has not reached the source.", isTransient: true))
            : Result.Success;
    }
}
