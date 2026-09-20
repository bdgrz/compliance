using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

public sealed record ProgramScopeManifest(int FormatVersion, Uuid TenantId, Uuid ProgramId,
    long ProgramRevision, string ProgramContentSha256, Uuid BoundaryId,
    Uuid ApprovedBoundaryVersionId, string BoundaryContentSha256);

public sealed record SnapshotRegistration(Uuid SnapshotId, string ContentSha256);

public sealed record SnapshotView(Uuid TenantId, Uuid SnapshotId, Uuid RootSnapshotId,
    Uuid? AmendsSnapshotId, Uuid ProgramId, string Kind, long Revision,
    ProgramScopeManifest Manifest, string CanonicalManifest, string ContentSha256,
    string? AmendmentReason, Uuid ActorMemberId, string ActorDisplay,
    DateTimeOffset FrozenAt);

public sealed record SnapshotSourceCheck(string Status, string ExpectedContentSha256,
    string? ObservedContentSha256);

public sealed record ProgramScopeSnapshotVerification(Uuid TenantId, Uuid SnapshotId,
    bool Verified, SnapshotSourceCheck Snapshot, SnapshotSourceCheck ProgramRevision,
    SnapshotSourceCheck ApprovedBoundaryVersion);

[Discriminator("bdgrz.snapshot.program_scope.freeze", 1)]
public sealed record FreezeProgramScopeSnapshot(Uuid TenantId, Uuid ProgramId,
    long ExpectedProgramRevision, Uuid BoundaryId, Uuid ApprovedBoundaryVersionId)
    : IRequest<SnapshotRegistration>, IProgramManagementRequest, ICallable;

[Discriminator("bdgrz.snapshot.program_scope.amend", 1)]
public sealed record AmendProgramScopeSnapshot(Uuid TenantId, Uuid SnapshotId,
    Uuid ProgramId, long ExpectedProgramRevision, Uuid BoundaryId,
    Uuid ApprovedBoundaryVersionId, string Reason)
    : IRequest<SnapshotRegistration>, IProgramManagementRequest, ICallable;

[Discriminator("bdgrz.snapshot.get", 1)]
public sealed record GetSnapshot(Uuid TenantId, Uuid SnapshotId, long? MinimumRevision = null)
    : IRequest<SnapshotView>, ITenantAccessRequest, ICallable;

[Discriminator("bdgrz.snapshot.program_scope.verify", 1)]
public sealed record VerifyProgramScopeSnapshot(Uuid TenantId, Uuid SnapshotId)
    : IRequest<ProgramScopeSnapshotVerification>, ITenantAccessRequest, ICallable;

[Discriminator("bdgrz.snapshot.program.list", 1)]
public sealed record ListProgramSnapshots(Uuid TenantId, Uuid ProgramId,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<SnapshotView>>, ITenantAccessRequest, ICallable;

[Discriminator("bdgrz.snapshot.frozen", 1)]
public sealed record SnapshotFrozen(Uuid TenantId, Uuid SnapshotId, Uuid RootSnapshotId,
    Uuid? AmendsSnapshotId, Uuid ProgramId, string Kind, ProgramScopeManifest Manifest,
    string CanonicalManifest, string ContentSha256, string? AmendmentReason,
    Uuid ActorMemberId, string ActorDisplay, DateTimeOffset FrozenAt) : DomainEvent;
