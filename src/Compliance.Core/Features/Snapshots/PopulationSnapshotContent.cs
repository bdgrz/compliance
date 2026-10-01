using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

/// <summary>
///     A frozen population snapshot with its verified rows, read from the immutable event source.
///     Reads fail closed: a missing, misplaced, or altered chunk, a storage manifest mismatch, or
///     a content identity mismatch is a conflict rather than partial content.
/// </summary>
public sealed record PopulationSnapshotContent(PopulationSnapshot Snapshot,
    IReadOnlyList<PopulationRow> Rows)
{
    public static async ValueTask<Result<PopulationSnapshotContent>> ReadAsync(
        IAggregateReader reader, Uuid tenantId, Uuid snapshotId, string kind, CancellationToken ct)
    {
        var snapshot = await reader.HydrateAsync(new PopulationSnapshot(tenantId, snapshotId), ct)
            .ConfigureAwait(false);
        if (!snapshot.IsFrozen || snapshot.Kind != kind)
            return Failure(RequestErrorKind.NotFound, "The snapshot was not found.");
        if (snapshot.StoredChunkCount == 0)
            return snapshot.HasIntactContent
                ? Result<PopulationSnapshotContent>.Success(new(snapshot, snapshot.Rows))
                : Corrupt();
        if (!snapshot.HasIntactIdentity || snapshot.Rows.Count != 0 ||
            snapshot.StoredChunkCount > PopulationSnapshotStorage.MaximumChunks)
            return Corrupt();

        var rows = new List<PopulationRow>((int)Math.Min(snapshot.RowCount, PopulationContentIdentity.MaximumRows));
        var chunks = new List<PopulationStorageChunk>(snapshot.StoredChunkCount);
        for (var index = 0; index < snapshot.StoredChunkCount; index++)
        {
            var chunk = await reader.HydrateAsync(
                new PopulationSnapshotChunk(tenantId, snapshotId, index), ct).ConfigureAwait(false);
            if (!chunk.IsIntact(kind) || chunk.Stored is not { } stored)
                return Corrupt();
            chunks.Add(new PopulationStorageChunk(index, rows.Count, stored.Rows.Count, 0,
                stored.ChunkSha256));
            rows.AddRange(stored.Rows);
        }
        var digest = PopulationContentIdentity.Compute(kind, rows);
        return digest.IsSuccess && digest.Value.Sha256 == snapshot.ContentSha256 &&
               digest.Value.RowCount == snapshot.RowCount &&
               digest.Value.ChunkCount == snapshot.ChunkCount &&
               PopulationSnapshotStorage.ManifestSha256(kind, snapshot.RowCount, chunks) ==
               snapshot.StorageManifestSha256
            ? Result<PopulationSnapshotContent>.Success(new(snapshot, rows))
            : Corrupt();
    }

    static Result<PopulationSnapshotContent> Corrupt() =>
        Failure(RequestErrorKind.Conflict, "The stored snapshot failed integrity verification.");

    static Result<PopulationSnapshotContent> Failure(RequestErrorKind kind, string message) =>
        Result<PopulationSnapshotContent>.Failure(new RequestError(kind, message));
}
