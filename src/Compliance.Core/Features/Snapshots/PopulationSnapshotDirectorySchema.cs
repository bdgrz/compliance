using System.Globalization;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

static class PopulationSnapshotDirectorySchema
{
    // Newest first through an ascending scan: a descending scan bounded by a prefix returns no
    // rows from the broker (#498), so the time component is inverted instead.
    public static readonly KvDirectoryIndex<PopulationSnapshotSummary> ByKindNewestFirst = new(
        "by_kind_newest_first", 1, static snapshot =>
            [snapshot.Kind,
                (long.MaxValue - snapshot.FrozenAt.ToUniversalTime().Ticks)
                    .ToString("D20", CultureInfo.InvariantCulture),
                snapshot.SnapshotId.ToString()]);

    public static readonly KvDirectory<PopulationSnapshotSummary, Uuid> Directory = new(
        "population-snapshots", ComplianceCoreJsonContext.Default.PopulationSnapshotSummary,
        static snapshot => snapshot.SnapshotId,
        static snapshotId => [snapshotId.ToString()], [ByKindNewestFirst]);
}
