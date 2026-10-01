using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

/// <summary>
///     An immutable population snapshot: canonical, key-ordered rows whose content identity is the
///     domain-separated population SHA-256. Amendments are new snapshots linked to the original.
///     A population that fits one storage chunk keeps its rows inline. A larger population keeps
///     no rows here: <paramref name="StoredChunkCount" /> chunk events hold them, and
///     <paramref name="StorageManifestSha256" /> binds those chunks in order.
/// </summary>
[Discriminator("bdgrz.snapshot.population.frozen", 1)]
public sealed record PopulationSnapshotFrozen(Uuid TenantId, Uuid SnapshotId, Uuid RootSnapshotId,
    Uuid? AmendsSnapshotId, string Kind, IReadOnlyList<PopulationRow> Rows, long RowCount,
    int ChunkCount, string ContentSha256, string? AmendmentReason, ActorReference FrozenBy,
    DateTimeOffset FrozenAt, int StoredChunkCount = 0, string? StorageManifestSha256 = null)
    : DomainEvent;
