using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.E2E;

/// <summary>
/// Observes durable source events and a projector checkpoint on the same stream pattern.
/// Event cursors are opaque, so the gate checks whether required event IDs remain after
/// the projector's committed cursor instead of comparing cursor strings.
/// </summary>
sealed class BrokerProjectionGate(IDomainEventReader events, IProjectionStore store,
    CheckpointIdentity identity)
{
    EventCursor _sourceCursor = EventCursor.Start;

    internal async ValueTask<IReadOnlyList<DomainEventRecord>> ReadNewEventsAsync(
        CancellationToken ct = default)
    {
        var records = new List<DomainEventRecord>();
        await foreach (var record in events.ReadAsync(identity.Pattern, _sourceCursor, ct)
                           .WithCancellation(ct))
        {
            records.Add(record);
            _sourceCursor = record.NextCursor;
        }

        return records;
    }

    internal async ValueTask<(ProjectionCheckpoint Checkpoint, bool Reached)> ObserveAsync(
        IReadOnlySet<Uuid> requiredEventIds, CancellationToken ct = default)
    {
        var checkpoint = await store.LoadCheckpointAsync(identity, ct);
        if (requiredEventIds.Count == 0)
            return (checkpoint, false);

        await foreach (var record in events.ReadAsync(identity.Pattern, checkpoint.Cursor, ct)
                           .WithCancellation(ct))
        {
            if (requiredEventIds.Contains(record.Event.Metadata.EventId))
                return (checkpoint, false);
        }

        return (checkpoint, true);
    }

    internal IAsyncEnumerable<DomainEventRecord> ReadPendingAsync(
        ProjectionCheckpoint checkpoint, CancellationToken ct = default) =>
        events.ReadAsync(identity.Pattern, checkpoint.Cursor, ct);
}
