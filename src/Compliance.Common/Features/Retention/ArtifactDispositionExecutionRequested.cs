using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

/// <summary>Records the ArtifactDispositionExecutionRequested domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.retention.artifact-disposition-execution-requested", 1)]
public sealed record ArtifactDispositionExecutionRequested(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
