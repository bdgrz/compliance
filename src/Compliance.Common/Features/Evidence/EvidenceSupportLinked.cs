using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Records the EvidenceSupportLinked domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.evidence.evidence-support-linked", 1)]
public sealed record EvidenceSupportLinked(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
