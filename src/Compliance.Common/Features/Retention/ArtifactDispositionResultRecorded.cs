using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

/// <summary>Records the ArtifactDispositionResultRecorded domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.retention.artifact-disposition-result-recorded", 1)]
public sealed record ArtifactDispositionResultRecorded(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
