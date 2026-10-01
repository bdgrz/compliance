using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

/// <summary>
///     Deterministic package-manifest regeneration for a population snapshot. The manifest is
///     recomputed from the verified retained rows and lineage, never from a projection, so the
///     same snapshot always regenerates byte-identical canonical v1 JSON and the same digest.
///     The digest is SHA-256 over <c>bdgrz.snapshot.manifest.population.v1\0</c> and that JSON.
/// </summary>
public static class PopulationSnapshotManifest
{
    const int FormatVersion = 1;

    static readonly byte[] ManifestDomain =
        Encoding.UTF8.GetBytes("bdgrz.snapshot.manifest.population.v1\0");

    public static async ValueTask<Result<PopulationSnapshotManifestRegeneration>> RegenerateAsync(
        IAggregateReader reader, Uuid tenantId, Uuid snapshotId, string kind, CancellationToken ct)
    {
        var content = await PopulationSnapshotContent.ReadAsync(reader, tenantId, snapshotId, kind,
            ct).ConfigureAwait(false);
        if (!content.IsSuccess)
            return Result<PopulationSnapshotManifestRegeneration>.Failure(content.Error);
        var snapshot = content.Value.Snapshot;
        if (!await PopulationSnapshotLineage.IsBoundedAsync(reader, tenantId, snapshot,
                SnapshotAmendmentLineage.MaximumAmendmentLinks, ct).ConfigureAwait(false))
            return Result<PopulationSnapshotManifestRegeneration>.Failure(new RequestError(
                RequestErrorKind.Conflict,
                "The snapshot amendment lineage is invalid or exceeds the supported depth."));

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, SnapshotContentIdentity.CanonicalWriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteNumber("format_version", FormatVersion);
            writer.WriteString("kind", kind);
            writer.WriteString("tenant_id", tenantId.ToString());
            writer.WriteString("snapshot_id", snapshot.Id.ToString());
            writer.WriteString("root_snapshot_id", snapshot.RootSnapshotId.ToString());
            if (snapshot.AmendsSnapshotId is { } amends)
                writer.WriteString("amends_snapshot_id", amends.ToString());
            else
                writer.WriteNull("amends_snapshot_id");
            writer.WriteNumber("row_count", snapshot.RowCount);
            writer.WriteNumber("chunk_count", snapshot.ChunkCount);
            writer.WriteString("content_sha256", snapshot.ContentSha256);
            writer.WriteEndObject();
        }
        var canonical = stream.ToArray();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(ManifestDomain);
        hash.AppendData(canonical);
        return Result<PopulationSnapshotManifestRegeneration>.Success(
            new PopulationSnapshotManifestRegeneration(tenantId, snapshot.Id, kind,
                snapshot.RowCount, snapshot.ChunkCount, snapshot.ContentSha256!,
                Encoding.UTF8.GetString(canonical), Convert.ToHexStringLower(hash.GetHashAndReset())));
    }
}
