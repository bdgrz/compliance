using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

/// <summary>Records the ArtifactEngagementHoldReleased domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.retention.artifact-engagement-hold-released", 1)]
public sealed record ArtifactEngagementHoldReleased(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
