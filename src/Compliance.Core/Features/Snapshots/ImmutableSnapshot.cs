using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

public sealed class ImmutableSnapshot : Aggregate
{
    readonly Uuid _tenantId;
    bool _isFrozen;
    Uuid _rootSnapshotId;
    Uuid? _amendsSnapshotId;
    Uuid _programId;
    string? _kind;
    ProgramScopeManifest? _manifest;
    string? _contentSha256;
    string? _canonicalManifest;
    string? _amendmentReason;
    Uuid _eventTenantId;
    Uuid _eventSnapshotId;
    Uuid _actorMemberId;
    string? _actorDisplay;
    DateTimeOffset _frozenAt;

    public bool IsFrozen => _isFrozen;
    public Uuid RootSnapshotId => _rootSnapshotId;
    public Uuid? AmendsSnapshotId => _amendsSnapshotId;
    public Uuid ProgramId => _programId;
    public string? Kind => _kind;
    public ProgramScopeManifest? Manifest => _manifest;
    public string? CanonicalManifest => _canonicalManifest;
    public string? ContentSha256 => _contentSha256;
    public string? AmendmentReason => _amendmentReason;
    public Uuid EventTenantId => _eventTenantId;
    public Uuid EventSnapshotId => _eventSnapshotId;
    public Uuid ActorMemberId => _actorMemberId;
    public string? ActorDisplay => _actorDisplay;
    public DateTimeOffset FrozenAt => _frozenAt;

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
            _kind = ev.Kind;
            _manifest = ev.Manifest;
            _contentSha256 = ev.ContentSha256;
            _canonicalManifest = ev.CanonicalManifest;
            _amendmentReason = ev.AmendmentReason;
            _eventTenantId = ev.TenantId;
            _eventSnapshotId = ev.SnapshotId;
            _actorMemberId = ev.ActorMemberId;
            _actorDisplay = ev.ActorDisplay;
            _frozenAt = ev.FrozenAt;
        });
    }

    public Result<SnapshotRegistration> Freeze(Uuid rootSnapshotId, Uuid? amendsSnapshotId,
        Uuid programId, ProgramScopeManifest manifest, string canonicalManifest,
        string contentSha256, string? amendmentReason, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset frozenAt)
    {
        amendmentReason = amendmentReason?.Trim();
        if (rootSnapshotId == Uuid.Empty || programId == Uuid.Empty ||
            actorMemberId == Uuid.Empty || string.IsNullOrWhiteSpace(canonicalManifest) ||
            string.IsNullOrWhiteSpace(contentSha256) ||
            (amendsSnapshotId is null) != (amendmentReason is null) ||
            (amendmentReason is not null && string.IsNullOrWhiteSpace(amendmentReason)) ||
            (amendsSnapshotId is null && rootSnapshotId != Id) ||
            (amendsSnapshotId is not null &&
             (amendsSnapshotId == Id || rootSnapshotId == Id)) ||
            manifest.TenantId != _tenantId || manifest.ProgramId != programId ||
            !SnapshotContentIdentity.MatchesManifest(manifest, canonicalManifest, contentSha256))
            return Result<SnapshotRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                "The snapshot requires a complete source manifest and amendment linkage."));
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
        RaiseEvent(new SnapshotFrozen(_tenantId, Id, rootSnapshotId, amendsSnapshotId,
            programId, "program_scope", manifest, canonicalManifest, contentSha256,
            amendmentReason?.Trim(), actorMemberId, actorDisplay, frozenAt));
        return Result<SnapshotRegistration>.Success(new SnapshotRegistration(Id, contentSha256));
    }
}
