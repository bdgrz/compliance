using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

sealed class FitzPopulationSnapshotDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/population-snapshot-directory-v2/projection",
            "PopulationSnapshotDirectoryV2"),
        IPopulationSnapshotDirectoryReader, IPopulationSnapshotDirectoryProjection
{
    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity("PopulationSnapshotDirectoryV2",
            EventStreamPattern.ForPattern(tenantId.ToString(), "population-snapshots")), ct);

    public ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        if (domainEvent is not PopulationSnapshotFrozen frozen)
            throw new ArgumentException("The population snapshot directory requires a frozen snapshot event.",
                nameof(domainEvent));
        return PopulationSnapshotDirectorySchema.Directory.InsertAsync(Transaction,
            new PopulationSnapshotSummary(frozen.TenantId, frozen.SnapshotId, frozen.RootSnapshotId,
                frozen.AmendsSnapshotId, frozen.Kind, frozen.RowCount, frozen.ContentSha256,
                frozen.AmendmentReason, frozen.FrozenBy, frozen.FrozenAt), ct);
    }

    public async ValueTask<Page<PopulationSnapshotSummary>> ListAsync(Uuid tenantId, string kind,
        int limit, string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await PopulationSnapshotDirectorySchema.Directory.QueryAsync(tx,
            PopulationSnapshotDirectorySchema.ByKindNewestFirst.Query().WithPrefix(kind)
                .Take(Math.Clamp(limit, 1, 200)).After(cursor), ct).ConfigureAwait(false);
    }
}
