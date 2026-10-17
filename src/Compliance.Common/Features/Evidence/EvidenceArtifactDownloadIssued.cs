using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Records the EvidenceArtifactDownloadIssued domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.evidence.evidence-artifact-download-issued", 1)]
public sealed record EvidenceArtifactDownloadIssued(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
