using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

/// <summary>Records the ArtifactHandlingPolicyAssigned domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.retention.artifact-handling-policy-assigned", 1)]
public sealed record ArtifactHandlingPolicyAssigned(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
