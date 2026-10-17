using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Records the AuditObservationFindingLinked domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.examinations.audit-observation-finding-linked", 1)]
public sealed record AuditObservationFindingLinked(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
