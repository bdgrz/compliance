using System.Globalization;
using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

public interface ISnapshotDirectoryReader
{
    ValueTask<SnapshotView?> GetAsync(Uuid tenantId, Uuid snapshotId,
        CancellationToken ct = default);
    ValueTask<Page<SnapshotView>> ListProgramAsync(Uuid tenantId, Uuid programId,
        int limit, string? cursor, CancellationToken ct = default);
}

public interface ISnapshotDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(SnapshotFrozen frozen, CancellationToken ct = default);
}

static class SnapshotDirectorySchema
{
    public static readonly KvDirectoryIndex<SnapshotView> ByProgram = new(
        "by_program", 1, static snapshot =>
            [snapshot.ProgramId.ToString(),
                snapshot.FrozenAt.ToUniversalTime().Ticks.ToString("D20", CultureInfo.InvariantCulture),
                snapshot.SnapshotId.ToString()]);

    public static readonly KvDirectory<SnapshotView, Uuid> Directory = new(
        "snapshots", ComplianceCoreJsonContext.Default.SnapshotView,
        static snapshot => snapshot.SnapshotId,
        static snapshotId => [snapshotId.ToString()], [ByProgram]);
}

sealed class FitzSnapshotDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/snapshot-directory/projection", "SnapshotDirectory"),
      ISnapshotDirectoryReader, ISnapshotDirectoryProjection
{
    public ValueTask ApplyAsync(SnapshotFrozen frozen, CancellationToken ct = default) =>
        SnapshotDirectorySchema.Directory.InsertAsync(Transaction,
            new SnapshotView(frozen.TenantId, frozen.SnapshotId, frozen.RootSnapshotId,
                frozen.AmendsSnapshotId, frozen.ProgramId, frozen.Kind, 1,
                frozen.Manifest, frozen.CanonicalManifest, frozen.ContentSha256,
                frozen.AmendmentReason, frozen.ActorMemberId, frozen.ActorDisplay,
                frozen.FrozenAt), ct);

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
