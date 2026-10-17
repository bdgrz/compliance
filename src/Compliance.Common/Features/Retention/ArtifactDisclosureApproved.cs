using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

/// <summary>Records the ArtifactDisclosureApproved domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.retention.artifact-disclosure-approved", 1)]
public sealed record ArtifactDisclosureApproved(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
