using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

/// <summary>Records the ArtifactDisclosureRevoked domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.retention.artifact-disclosure-revoked", 1)]
public sealed record ArtifactDisclosureRevoked(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
