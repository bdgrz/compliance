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
    public async ValueTask<Result> EnsureCaughtUpAsync(Uuid tenantId, CancellationToken ct)
    {
        var checkpoint = await directory.LoadCheckpointAsync(tenantId, ct).ConfigureAwait(false);
        await using var pending = events.ReadAsync(
                EventStreamPattern.ForPattern(tenantId.ToString(), "boundaries"),
                checkpoint.Cursor, ct).GetAsyncEnumerator(ct);
        return await pending.MoveNextAsync().ConfigureAwait(false)
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The boundary reference projection has not reached the source. Retry the query.",
                isTransient: true))
            : Result.Success;
    }
}
