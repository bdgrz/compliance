using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

/// <summary>A frozen population snapshot's identity and lineage, without its rows.</summary>
public sealed record PopulationSnapshotSummary(Uuid TenantId, Uuid SnapshotId, Uuid RootSnapshotId,
    Uuid? AmendsSnapshotId, string Kind, long RowCount, string ContentSha256,
    string? AmendmentReason, ActorReference FrozenBy, DateTimeOffset FrozenAt);
