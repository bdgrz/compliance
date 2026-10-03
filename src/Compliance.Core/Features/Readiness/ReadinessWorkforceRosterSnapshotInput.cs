using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>Identity metadata for the integrity-verified workforce roster snapshot selected as of a run.</summary>
public sealed record ReadinessWorkforceRosterSnapshotInput(Uuid SnapshotId,
    string ContentSha256, DateTimeOffset FrozenAt, long RowCount);
