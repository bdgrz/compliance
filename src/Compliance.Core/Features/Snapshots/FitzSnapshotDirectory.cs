using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

sealed class FitzSnapshotDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/snapshot-directory/projection", "SnapshotDirectory"),
        ISnapshotDirectoryReader, ISnapshotDirectoryProjection
{
    public ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        if (domainEvent is not SnapshotFrozen frozen)
            throw new ArgumentException("The snapshot directory requires a frozen snapshot event.",
                nameof(domainEvent));
        return SnapshotDirectorySchema.Directory.InsertAsync(Transaction,
            new SnapshotView(frozen.TenantId, frozen.SnapshotId, frozen.RootSnapshotId,
                frozen.AmendsSnapshotId, frozen.ProgramId, frozen.Kind, 1,
                frozen.Manifest, frozen.CanonicalManifest, frozen.ContentSha256,
                frozen.AmendmentReason, frozen.ActorMemberId, frozen.ActorDisplay,
                frozen.FrozenAt), ct);
    }

    public async ValueTask<SnapshotView?> GetAsync(Uuid tenantId, Uuid snapshotId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await SnapshotDirectorySchema.Directory.GetAsync(tx, snapshotId, ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<Page<SnapshotView>> ListProgramAsync(Uuid tenantId,
        Uuid programId, int limit, string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await SnapshotDirectorySchema.Directory.QueryAsync(tx,
            SnapshotDirectorySchema.ByProgram.Query().WithPrefix(programId.ToString())
                .Take(Math.Clamp(limit, 1, 200)).After(cursor), ct).ConfigureAwait(false);
    }
}
