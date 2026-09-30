using System.Globalization;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

static class PopulationSnapshotDirectorySchema
{
    public static readonly KvDirectoryIndex<PopulationSnapshotSummary> ByKindFrozenAt = new(
        "by_kind_frozen_at", 1, static snapshot =>
            [snapshot.Kind,
                snapshot.FrozenAt.ToUniversalTime().Ticks.ToString("D20", CultureInfo.InvariantCulture),
                snapshot.SnapshotId.ToString()]);

    public static readonly KvDirectory<PopulationSnapshotSummary, Uuid> Directory = new(
        "population-snapshots", ComplianceCoreJsonContext.Default.PopulationSnapshotSummary,
        static snapshot => snapshot.SnapshotId,
        static snapshotId => [snapshotId.ToString()], [ByKindFrozenAt]);
}
