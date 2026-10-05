using Bdgrz.Compliance.Features.Boundaries;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Fences a complete queue read against tenant changes and projected evidence work.</summary>
public sealed class WorkQueueReadConsistency(IBoundaryDirectoryReader tenantCheckpoint,
    IDomainEventReader events, IEvidenceWorkItemDirectoryReader? evidenceWorkItems = null)
{
    public async ValueTask<Result<WorkQueueReadFence>> CaptureAsync(Uuid tenantId,
        CancellationToken ct)
    {
        var tenantCursor = await tenantCheckpoint.LoadCheckpointAsync(tenantId, ct)
            .ConfigureAwait(false);
        if (await HasPendingSourceAsync(EventStreamPattern.ForPattern(tenantId.ToString()),
                tenantCursor, ct).ConfigureAwait(false))
            return Result<WorkQueueReadFence>.Failure(BehindSourceError());

        ProjectionCheckpoint? evidenceCursor = null;
        if (evidenceWorkItems is not null)
        {
            evidenceCursor = await evidenceWorkItems.LoadCheckpointAsync(tenantId, ct)
                .ConfigureAwait(false);
            if (await HasPendingSourceAsync(EvidencePattern(tenantId), evidenceCursor.Value, ct)
                    .ConfigureAwait(false))
                return Result<WorkQueueReadFence>.Failure(BehindSourceError());
        }

        return Result<WorkQueueReadFence>.Success(new WorkQueueReadFence(tenantCursor,
            evidenceCursor));
    }

    public async ValueTask<Result> ConfirmUnchangedAndCaughtUpAsync(Uuid tenantId,
        WorkQueueReadFence fence, CancellationToken ct)
    {
        var tenantCursor = await tenantCheckpoint.LoadCheckpointAsync(tenantId, ct)
            .ConfigureAwait(false);
        if (tenantCursor != fence.TenantCheckpoint ||
            await HasPendingSourceAsync(EventStreamPattern.ForPattern(tenantId.ToString()),
                tenantCursor, ct).ConfigureAwait(false))
            return Result.Failure(BehindSourceError());

        if (evidenceWorkItems is null)
        {
            if (fence.EvidenceWorkItemCheckpoint is not null)
                return Result.Failure(BehindSourceError());
        }
        else
        {
            var evidenceCursor = await evidenceWorkItems.LoadCheckpointAsync(tenantId, ct)
                .ConfigureAwait(false);
            if (fence.EvidenceWorkItemCheckpoint is not { } evidenceFence ||
                evidenceCursor != evidenceFence ||
                await HasPendingSourceAsync(EvidencePattern(tenantId), evidenceCursor, ct)
                    .ConfigureAwait(false))
                return Result.Failure(BehindSourceError());
        }

        return Result.Success;
    }

    async ValueTask<bool> HasPendingSourceAsync(EventStreamPattern pattern,
        ProjectionCheckpoint checkpoint, CancellationToken ct)
    {
        await using var pending = events.ReadAsync(pattern, checkpoint.Cursor, ct)
            .GetAsyncEnumerator(ct);
        return await pending.MoveNextAsync().ConfigureAwait(false);
    }

    static EventStreamPattern EvidencePattern(Uuid tenantId) =>
        EventStreamPattern.ForPattern(tenantId.ToString(), "evidence-requests");

    static RequestError BehindSourceError() => new(RequestErrorKind.Conflict,
        "The work queue sources changed or a required projection is behind. Retry the query.",
        isTransient: true);
}
