using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

[Discriminator("bdgrz.snapshot.frozen", 1)]
public sealed record SnapshotFrozen(Uuid TenantId, Uuid SnapshotId, Uuid RootSnapshotId,
    Uuid? AmendsSnapshotId, Uuid ProgramId, string Kind, ProgramScopeManifest Manifest,
    string CanonicalManifest, string ContentSha256, string? AmendmentReason,
    Uuid ActorMemberId, string ActorDisplay, DateTimeOffset FrozenAt) : DomainEvent;
