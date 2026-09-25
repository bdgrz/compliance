using System.Globalization;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

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
