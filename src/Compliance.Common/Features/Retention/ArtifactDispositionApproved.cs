using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

/// <summary>Records the ArtifactDispositionApproved domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.retention.artifact-disposition-approved", 1)]
public sealed record ArtifactDispositionApproved(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
