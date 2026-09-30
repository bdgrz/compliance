using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

public interface IPopulationSnapshotDirectoryReader
{
    /// <summary>Lists one kind's snapshots, newest frozen first.</summary>
    ValueTask<Page<PopulationSnapshotSummary>> ListAsync(Uuid tenantId, string kind, int limit,
        string? cursor, CancellationToken ct = default);
}
