using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Records the AuditObservationRecorded domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.examinations.audit-observation-recorded", 1)]
public sealed record AuditObservationRecorded(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
