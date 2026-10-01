using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

/// <summary>
///     A population snapshot's canonical v1 package manifest, recomputed from its retained rows.
///     Regenerating the same snapshot always yields the same bytes and digest.
/// </summary>
public sealed record PopulationSnapshotManifestRegeneration(Uuid TenantId, Uuid SnapshotId,
    string Kind, long RowCount, int ChunkCount, string ContentSha256, string CanonicalManifest,
    string ManifestSha256);
