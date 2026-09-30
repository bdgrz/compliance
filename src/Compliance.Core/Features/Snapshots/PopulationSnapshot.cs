using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

/// <summary>
///     The reusable immutable population snapshot (EN-03). A snapshot is frozen once with
///     canonical rows and their content identity; corrections are new, attributable amendment
///     snapshots that link to the original and its lineage root without changing either.
/// </summary>
public sealed class PopulationSnapshot : Aggregate
{
    /// <summary>
    ///     Rows are retained inline in the frozen event. Chunked row storage is required before this
    ///     bound can grow toward <see cref="PopulationContentIdentity.MaximumRows" />.
    /// </summary>
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
    public string? ContentSha256 => _frozen?.ContentSha256;
    public string? AmendmentReason => _frozen?.AmendmentReason;
    public ActorReference? FrozenBy => _frozen?.FrozenBy;
    public DateTimeOffset FrozenAt => _frozen?.FrozenAt ?? default;
    public IReadOnlyList<PopulationRow> Rows => _frozen?.Rows ?? [];

    /// <summary>True when the retained event belongs to this stream and its rows still hash to its identity.</summary>
    public bool HasIntactContent =>
        _frozen is { } frozen && frozen.TenantId == _tenantId && frozen.SnapshotId == Id &&
        PopulationContentIdentity.Compute(frozen.Kind, frozen.Rows) is { IsSuccess: true } digest &&
        digest.Value.Sha256 == frozen.ContentSha256 && digest.Value.RowCount == frozen.RowCount;

    public Result<SnapshotRegistration> Freeze(Uuid rootSnapshotId, Uuid? amendsSnapshotId,
        string kind, IReadOnlyList<PopulationRow> rows, string contentSha256,
        string? amendmentReason, ActorReference frozenBy, DateTimeOffset frozenAt)
    {
        amendmentReason = amendmentReason?.Trim();
        if (rows is null || frozenBy is null || rows.Count > MaximumInlineRows ||
            rootSnapshotId == Uuid.Empty ||
            (amendsSnapshotId is null) != (amendmentReason is null) ||
            amendmentReason is { Length: 0 or > 4000 } ||
            (amendsSnapshotId is null && rootSnapshotId != Id) ||
            (amendsSnapshotId is not null && (amendsSnapshotId == Id || rootSnapshotId == Id)))
            return Failure(RequestErrorKind.Validation,
                "The snapshot requires bounded rows and valid amendment linkage.");
        var digest = PopulationContentIdentity.Compute(kind, rows);
        if (!digest.IsSuccess)
            return Result<SnapshotRegistration>.Failure(digest.Error);
        if (!string.Equals(digest.Value.Sha256, contentSha256, StringComparison.Ordinal))
            return Failure(RequestErrorKind.Validation,
                "The snapshot content identity does not match its rows.");
        if (_frozen is { } frozen)
            return frozen.RootSnapshotId == rootSnapshotId &&
                   frozen.AmendsSnapshotId == amendsSnapshotId && frozen.Kind == kind &&
                   frozen.ContentSha256 == contentSha256 &&
                   frozen.AmendmentReason == amendmentReason &&
                   frozen.FrozenBy.Id == frozenBy.Id
                ? Result<SnapshotRegistration>.Success(new SnapshotRegistration(Id, contentSha256))
                : Failure(RequestErrorKind.Conflict,
                    "The snapshot already exists with different content.");
        RaiseEvent(new PopulationSnapshotFrozen(_tenantId, Id, rootSnapshotId, amendsSnapshotId,
            kind, [.. rows], digest.Value.RowCount, digest.Value.ChunkCount, contentSha256,
            amendmentReason, frozenBy, frozenAt));
        return Result<SnapshotRegistration>.Success(new SnapshotRegistration(Id, contentSha256));
    }

    static Result<SnapshotRegistration> Failure(RequestErrorKind kind, string message) =>
        Result<SnapshotRegistration>.Failure(new RequestError(kind, message));
}
