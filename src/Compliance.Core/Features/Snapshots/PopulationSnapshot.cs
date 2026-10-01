using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

/// <summary>
///     The reusable immutable population snapshot (EN-03). A snapshot is frozen once with
///     canonical rows and their content identity; corrections are new, attributable amendment
///     snapshots that link to the original and its lineage root without changing either.
///     A population that fits one storage chunk is frozen inline. A larger population is frozen
///     as a manifest over <see cref="PopulationSnapshotChunk" /> streams, which
///     <see cref="PopulationSnapshotContent" /> reads and verifies.
/// </summary>
public sealed class PopulationSnapshot : Aggregate
{
    /// <summary>The most rows an inline freeze may retain in its single frozen event.</summary>
    internal const int MaximumInlineRows = 500;

    readonly Uuid _tenantId;
    PopulationSnapshotFrozen? _frozen;

    public PopulationSnapshot(Uuid tenantId, Uuid snapshotId)
        : base(snapshotId, new EventStreamAddress(tenantId.ToString(), "population-snapshots",
            snapshotId.ToString()))
    {
        _tenantId = tenantId;
        On<PopulationSnapshotFrozen>(ev => _frozen = ev);
    }

    public bool IsFrozen => _frozen is not null;
    public Uuid RootSnapshotId => _frozen?.RootSnapshotId ?? Uuid.Empty;
    public Uuid? AmendsSnapshotId => _frozen?.AmendsSnapshotId;
    public string? Kind => _frozen?.Kind;
    public long RowCount => _frozen?.RowCount ?? 0;
    public int ChunkCount => _frozen?.ChunkCount ?? 0;
    public string? ContentSha256 => _frozen?.ContentSha256;
    public string? AmendmentReason => _frozen?.AmendmentReason;
    public ActorReference? FrozenBy => _frozen?.FrozenBy;
    public DateTimeOffset FrozenAt => _frozen?.FrozenAt ?? default;

    /// <summary>The inline rows; empty when the rows are held in storage chunks.</summary>
    public IReadOnlyList<PopulationRow> Rows => _frozen?.Rows ?? [];

    /// <summary>The number of storage chunks, or zero for an inline snapshot.</summary>
    public int StoredChunkCount => _frozen?.StoredChunkCount ?? 0;
    public string? StorageManifestSha256 => _frozen?.StorageManifestSha256;

    /// <summary>True when the retained event belongs to this tenant stream.</summary>
    public bool HasIntactIdentity =>
        _frozen is { } frozen && frozen.TenantId == _tenantId && frozen.SnapshotId == Id;

    /// <summary>
    ///     True for an inline snapshot whose retained rows still hash to its identity. A chunked
    ///     snapshot is verified with its chunks by <see cref="PopulationSnapshotContent" />.
    /// </summary>
    public bool HasIntactContent =>
        _frozen is { StoredChunkCount: 0 } frozen && HasIntactIdentity &&
        PopulationContentIdentity.Compute(frozen.Kind, frozen.Rows) is { IsSuccess: true } digest &&
        digest.Value.Sha256 == frozen.ContentSha256 && digest.Value.RowCount == frozen.RowCount;

    /// <summary>Freezes a population small enough for one event inline.</summary>
    public Result<SnapshotRegistration> Freeze(Uuid rootSnapshotId, Uuid? amendsSnapshotId,
        string kind, IReadOnlyList<PopulationRow> rows, string contentSha256,
        string? amendmentReason, ActorReference frozenBy, DateTimeOffset frozenAt)
    {
        amendmentReason = amendmentReason?.Trim();
        if (rows is null || rows.Count > MaximumInlineRows ||
            !HasValidLinkage(rootSnapshotId, amendsSnapshotId, amendmentReason, frozenBy))
            return Failure(RequestErrorKind.Validation,
                "The snapshot requires bounded rows and valid amendment linkage.");
        var digest = PopulationContentIdentity.Compute(kind, rows);
        if (!digest.IsSuccess)
            return Result<SnapshotRegistration>.Failure(digest.Error);
        if (!string.Equals(digest.Value.Sha256, contentSha256, StringComparison.Ordinal))
            return Failure(RequestErrorKind.Validation,
                "The snapshot content identity does not match its rows.");
        var plan = PopulationSnapshotStorage.Plan(kind, rows);
        if (!plan.IsSuccess)
            return Result<SnapshotRegistration>.Failure(plan.Error);
        if (plan.Value.Count > 1)
            return Failure(RequestErrorKind.Validation,
                "The population exceeds one storage chunk and must be frozen in chunks.");
        if (_frozen is not null)
            return Replay(rootSnapshotId, amendsSnapshotId, kind, contentSha256, amendmentReason,
                frozenBy, null);
        RaiseEvent(new PopulationSnapshotFrozen(_tenantId, Id, rootSnapshotId, amendsSnapshotId,
            kind, [.. rows], digest.Value.RowCount, digest.Value.ChunkCount, contentSha256,
            amendmentReason, frozenBy, frozenAt));
        return Result<SnapshotRegistration>.Success(new SnapshotRegistration(Id, contentSha256));
    }

    /// <summary>
    ///     Freezes the manifest of a population whose rows were already stored, in order, as the
    ///     planned chunks. The manifest commit is what makes the snapshot visible.
    /// </summary>
    public Result<SnapshotRegistration> FreezeChunked(Uuid rootSnapshotId, Uuid? amendsSnapshotId,
        string kind, PopulationDigest digest, IReadOnlyList<PopulationStorageChunk> chunks,
        string? amendmentReason, ActorReference frozenBy, DateTimeOffset frozenAt)
    {
        amendmentReason = amendmentReason?.Trim();
        if (digest is null || chunks is null || chunks.Count is < 2 or > PopulationSnapshotStorage.MaximumChunks ||
            chunks.Sum(chunk => (long)chunk.Count) != digest.RowCount ||
            !HasValidLinkage(rootSnapshotId, amendsSnapshotId, amendmentReason, frozenBy))
            return Failure(RequestErrorKind.Validation,
                "The snapshot requires ordered storage chunks and valid amendment linkage.");
        for (var index = 0; index < chunks.Count; index++)
        {
            if (chunks[index].Index != index)
                return Failure(RequestErrorKind.Validation,
                    "The snapshot storage chunks must be ordered.");
        }
        var manifest = PopulationSnapshotStorage.ManifestSha256(kind, digest.RowCount, chunks);
        if (_frozen is not null)
            return Replay(rootSnapshotId, amendsSnapshotId, kind, digest.Sha256, amendmentReason,
                frozenBy, manifest);
        RaiseEvent(new PopulationSnapshotFrozen(_tenantId, Id, rootSnapshotId, amendsSnapshotId,
            kind, [], digest.RowCount, digest.ChunkCount, digest.Sha256, amendmentReason, frozenBy,
            frozenAt, chunks.Count, manifest));
        return Result<SnapshotRegistration>.Success(new SnapshotRegistration(Id, digest.Sha256));
    }

    bool HasValidLinkage(Uuid rootSnapshotId, Uuid? amendsSnapshotId, string? amendmentReason,
        ActorReference frozenBy) =>
        frozenBy is not null && rootSnapshotId != Uuid.Empty &&
        (amendsSnapshotId is null) == (amendmentReason is null) &&
        amendmentReason is not { Length: 0 or > 4000 } &&
        (amendsSnapshotId is not null || rootSnapshotId == Id) &&
        (amendsSnapshotId is null || (amendsSnapshotId != Id && rootSnapshotId != Id));

    Result<SnapshotRegistration> Replay(Uuid rootSnapshotId, Uuid? amendsSnapshotId, string kind,
        string contentSha256, string? amendmentReason, ActorReference frozenBy,
        string? storageManifestSha256)
    {
        var frozen = _frozen!;
        return frozen.RootSnapshotId == rootSnapshotId &&
               frozen.AmendsSnapshotId == amendsSnapshotId && frozen.Kind == kind &&
               frozen.ContentSha256 == contentSha256 &&
               frozen.AmendmentReason == amendmentReason &&
               frozen.FrozenBy.Id == frozenBy.Id &&
               frozen.StorageManifestSha256 == storageManifestSha256
            ? Result<SnapshotRegistration>.Success(new SnapshotRegistration(Id, contentSha256))
            : Failure(RequestErrorKind.Conflict,
                "The snapshot already exists with different content.");
    }

    static Result<SnapshotRegistration> Failure(RequestErrorKind kind, string message) =>
        Result<SnapshotRegistration>.Failure(new RequestError(kind, message));
}
