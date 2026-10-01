using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

/// <summary>
///     One immutable storage chunk of a population snapshot. Storing the same rows again replays;
///     different rows at the same position conflict.
/// </summary>
public sealed class PopulationSnapshotChunk : Aggregate
{
    public const string Category = "population-snapshot-chunks";

    static Uuid NamespaceId { get; } =
        Uuid.Parse("5b0f3f5e-8d4c-5a49-9a0e-3c1f9b6d2e71", CultureInfo.InvariantCulture);

    readonly Uuid _tenantId;
    readonly Uuid _snapshotId;
    PopulationSnapshotChunkStored? _stored;

    public PopulationSnapshotChunk(Uuid tenantId, Uuid snapshotId, int index)
        : base(IdFor(tenantId, snapshotId, index), new EventStreamAddress(tenantId.ToString(),
            Category, IdFor(tenantId, snapshotId, index).ToString()))
    {
        _tenantId = tenantId;
        _snapshotId = snapshotId;
        Index = index;
        On<PopulationSnapshotChunkStored>(ev => _stored = ev);
    }

    public int Index { get; }
    public PopulationSnapshotChunkStored? Stored => _stored;

    /// <summary>True when the retained chunk belongs to this position and its rows hash to its digest.</summary>
    public bool IsIntact(string kind) =>
        _stored is { } stored && stored.TenantId == _tenantId && stored.SnapshotId == _snapshotId &&
        stored.ChunkIndex == Index && stored.Kind == kind &&
        PopulationContentIdentity.Compute(kind, stored.Rows) is { IsSuccess: true } digest &&
        digest.Value.Sha256 == stored.ChunkSha256 && digest.Value.RowCount == stored.Rows.Count;

    public static Uuid IdFor(Uuid tenantId, Uuid snapshotId, int index) =>
        Uuid.CreateVersion5(NamespaceId,
            $"{tenantId}\n{snapshotId}\n{index.ToString(CultureInfo.InvariantCulture)}");

    public Result Store(string kind, IReadOnlyList<PopulationRow> rows, string chunkSha256)
    {
        if (Index < 0 || rows is null || rows.Count is 0 or > PopulationSnapshotStorage.MaximumRowsPerChunk)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A population storage chunk requires a valid position and bounded rows."));
        var digest = PopulationContentIdentity.Compute(kind, rows);
        if (!digest.IsSuccess)
            return Result.Failure(digest.Error);
        if (!string.Equals(digest.Value.Sha256, chunkSha256, StringComparison.Ordinal))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The storage chunk identity does not match its rows."));
        if (_stored is { } stored)
            return stored.Kind == kind && stored.ChunkSha256 == chunkSha256
                ? Result.Success
                : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The snapshot storage chunk already exists with different content."));
        RaiseEvent(new PopulationSnapshotChunkStored(_tenantId, _snapshotId, Index, kind, [.. rows],
            chunkSha256));
        return Result.Success;
    }
}
