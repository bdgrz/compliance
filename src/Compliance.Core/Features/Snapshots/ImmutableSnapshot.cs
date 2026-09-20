using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

public sealed class ImmutableSnapshot : Aggregate
{
    readonly Uuid _tenantId;
    bool _isFrozen;
    Uuid _rootSnapshotId;
    Uuid? _amendsSnapshotId;
    Uuid _programId;
    string? _contentSha256;
    string? _canonicalManifest;
    string? _amendmentReason;
    Uuid _actorMemberId;

    public bool IsFrozen => _isFrozen;
    public Uuid RootSnapshotId => _rootSnapshotId;
    public Uuid ProgramId => _programId;

    public ImmutableSnapshot(Uuid tenantId, Uuid snapshotId)
        : base(snapshotId, new EventStreamAddress(tenantId.ToString(), "snapshots",
            snapshotId.ToString()))
    {
        _tenantId = tenantId;
        On<SnapshotFrozen>(ev =>
        {
            _isFrozen = true;
            _rootSnapshotId = ev.RootSnapshotId;
            _amendsSnapshotId = ev.AmendsSnapshotId;
            _programId = ev.ProgramId;
            _contentSha256 = ev.ContentSha256;
            _canonicalManifest = ev.CanonicalManifest;
            _amendmentReason = ev.AmendmentReason;
            _actorMemberId = ev.ActorMemberId;
        });
    }

    public Result<SnapshotRegistration> Freeze(Uuid rootSnapshotId, Uuid? amendsSnapshotId,
        Uuid programId, ProgramScopeManifest manifest, string canonicalManifest,
        string contentSha256, string? amendmentReason, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset frozenAt)
    {
        amendmentReason = amendmentReason?.Trim();
        if (_isFrozen)
            return _rootSnapshotId == rootSnapshotId &&
                   _amendsSnapshotId == amendsSnapshotId &&
                   _programId == programId &&
                   _contentSha256 == contentSha256 &&
                   _canonicalManifest == canonicalManifest &&
                   _amendmentReason == amendmentReason &&
                   _actorMemberId == actorMemberId
                ? Result<SnapshotRegistration>.Success(new SnapshotRegistration(Id,
                    _contentSha256!))
                : Result<SnapshotRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The snapshot already exists with different content."));
        if (rootSnapshotId == Uuid.Empty || programId == Uuid.Empty ||
            actorMemberId == Uuid.Empty || string.IsNullOrWhiteSpace(canonicalManifest) ||
            string.IsNullOrWhiteSpace(contentSha256) ||
            (amendsSnapshotId is null) != (amendmentReason is null) ||
            (amendmentReason is not null && string.IsNullOrWhiteSpace(amendmentReason)) ||
            manifest.TenantId != _tenantId || manifest.ProgramId != programId ||
            !SnapshotContentIdentity.MatchesManifest(manifest, canonicalManifest, contentSha256))
            return Result<SnapshotRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                "The snapshot requires a complete source manifest and amendment linkage."));

        RaiseEvent(new SnapshotFrozen(_tenantId, Id, rootSnapshotId, amendsSnapshotId,
            programId, "program_scope", manifest, canonicalManifest, contentSha256,
            amendmentReason?.Trim(), actorMemberId, actorDisplay, frozenAt));
        return Result<SnapshotRegistration>.Success(new SnapshotRegistration(Id, contentSha256));
    }
}
