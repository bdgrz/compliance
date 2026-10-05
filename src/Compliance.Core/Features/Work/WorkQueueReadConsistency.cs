using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Fences a queue read against lagging or changing required source projections.</summary>
public sealed class WorkQueueReadConsistency(IDomainEventReader events,
    IEnumerable<IAccountableWorkItemDirectoryReader>? accountableWorkItems = null)
{
    readonly IAccountableWorkItemDirectoryReader[] _accountableWorkItems =
        accountableWorkItems?.OrderBy(static reader => reader.ProjectorName,
            StringComparer.Ordinal).ToArray() ?? [];

    public async ValueTask<Result<WorkQueueReadFence>> CaptureAsync(Uuid tenantId,
        CancellationToken ct)
    {
        var workItemProjections = new List<WorkItemProjectionFence>(_accountableWorkItems.Length);
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var workItems in _accountableWorkItems)
        {
            if (!identities.Add(workItems.ProjectorName))
                throw new InvalidOperationException(
                    $"The work queue has duplicate projector identity {workItems.ProjectorName}.");
            var pattern = workItems.SourcePattern(tenantId);
            var checkpoint = await workItems.LoadCheckpointAsync(tenantId, ct)
                .ConfigureAwait(false);
            if (await HasPendingSourceAsync(pattern, checkpoint, ct).ConfigureAwait(false))
                return Result<WorkQueueReadFence>.Failure(BehindSourceError());
            workItemProjections.Add(new WorkItemProjectionFence(workItems.ProjectorName,
                checkpoint));
        }

        return Result<WorkQueueReadFence>.Success(new WorkQueueReadFence(workItemProjections));
    }

    public async ValueTask<Result> ConfirmUnchangedAndCaughtUpAsync(Uuid tenantId,
        WorkQueueReadFence fence, CancellationToken ct)
    {
        if (fence.WorkItemProjections.Count != _accountableWorkItems.Length)
            return Result.Failure(BehindSourceError());

        foreach (var workItems in _accountableWorkItems)
        {
            var projectionFence = fence.WorkItemProjections.SingleOrDefault(item =>
                StringComparer.Ordinal.Equals(item.ProjectorName, workItems.ProjectorName));
            if (projectionFence is null)
                return Result.Failure(BehindSourceError());
            var pattern = workItems.SourcePattern(tenantId);
            var checkpoint = await workItems.LoadCheckpointAsync(tenantId, ct)
                .ConfigureAwait(false);
            if (checkpoint != projectionFence.Checkpoint ||
                await HasPendingSourceAsync(pattern, checkpoint, ct).ConfigureAwait(false))
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

    static RequestError BehindSourceError() => new(RequestErrorKind.Conflict,
        "The work queue sources changed or a required projection is behind. Retry the query.",
        isTransient: true);
}
