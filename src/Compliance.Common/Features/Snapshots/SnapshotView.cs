using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

public sealed record SnapshotView(Uuid TenantId, Uuid SnapshotId, Uuid RootSnapshotId,
    Uuid? AmendsSnapshotId, Uuid ProgramId, string Kind, long Revision,
    ProgramScopeManifest Manifest, string CanonicalManifest, string ContentSha256,
    string? AmendmentReason, Uuid ActorMemberId, string ActorDisplay,
    DateTimeOffset FrozenAt);
