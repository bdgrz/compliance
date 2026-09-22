using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Checks the Control source cursor before an application preview trusts this index.</summary>
public sealed class ApplicationControlDraftReferenceReadConsistency(
    IApplicationControlDraftReferenceDirectory directory, IDomainEventReader events)
{
    public async ValueTask<Result> EnsureCaughtUpAsync(Uuid tenantId, CancellationToken ct)
    {
        var checkpoint = await directory.LoadCheckpointAsync(tenantId, ct).ConfigureAwait(false);
        await using var pending = events.ReadAsync(
                EventStreamPattern.ForPattern(tenantId.ToString(), "controls"), checkpoint.Cursor, ct)
            .GetAsyncEnumerator(ct);
        return await pending.MoveNextAsync().ConfigureAwait(false)
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The application control draft reference projection has not reached the source.",
                isTransient: true))
            : Result.Success;
    }
}
