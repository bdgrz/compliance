using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

public sealed class ImmutableSnapshot : Aggregate
{
    readonly Uuid _tenantId;
    SnapshotFrozen? _frozen;

    public bool IsFrozen => _frozen is not null;
    public SnapshotFrozen Frozen => _frozen ?? throw new InvalidOperationException(
        "The snapshot has not been frozen.");

    public ImmutableSnapshot(Uuid tenantId, Uuid snapshotId)
        : base(snapshotId, new EventStreamAddress(tenantId.ToString(), "snapshots",
            snapshotId.ToString()))
    {
        _tenantId = tenantId;
        On<SnapshotFrozen>(ev => _frozen = ev);
    }

    public Result<SnapshotRegistration> Freeze(Uuid rootSnapshotId, Uuid? amendsSnapshotId,
        Uuid programId, ProgramScopeManifest manifest, string canonicalManifest,
        string contentSha256, string? amendmentReason, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset frozenAt)
    {
        amendmentReason = amendmentReason?.Trim();
        if (_frozen is { } prior)
            return prior.RootSnapshotId == rootSnapshotId &&
                   prior.AmendsSnapshotId == amendsSnapshotId &&
                   prior.ProgramId == programId &&
                   prior.ContentSha256 == contentSha256 &&
                   prior.AmendmentReason == amendmentReason
                ? Result<SnapshotRegistration>.Success(new SnapshotRegistration(Id,
                    prior.ContentSha256))
                : Result<SnapshotRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The snapshot already exists with different content."));
        if (rootSnapshotId == Uuid.Empty || programId == Uuid.Empty ||
            actorMemberId == Uuid.Empty || string.IsNullOrWhiteSpace(canonicalManifest) ||
            string.IsNullOrWhiteSpace(contentSha256) ||
            (amendsSnapshotId is null) != (amendmentReason is null) ||
            (amendmentReason is not null && string.IsNullOrWhiteSpace(amendmentReason)) ||
            manifest.TenantId != _tenantId || manifest.ProgramId != programId)
            return Result<SnapshotRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                "The snapshot requires a complete source manifest and amendment linkage."));

        RaiseEvent(new SnapshotFrozen(_tenantId, Id, rootSnapshotId, amendsSnapshotId,
            programId, "program_scope", manifest, canonicalManifest, contentSha256,
            amendmentReason?.Trim(), actorMemberId, actorDisplay, frozenAt));
        return Result<SnapshotRegistration>.Success(new SnapshotRegistration(Id, contentSha256));
    }
}
