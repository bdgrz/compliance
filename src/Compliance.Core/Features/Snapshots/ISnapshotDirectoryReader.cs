using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

public interface ISnapshotDirectoryReader
{
    ValueTask<SnapshotView?> GetAsync(Uuid tenantId, Uuid snapshotId,
        CancellationToken ct = default);
    ValueTask<Page<SnapshotView>> ListProgramAsync(Uuid tenantId, Uuid programId,
        int limit, string? cursor, CancellationToken ct = default);
}
