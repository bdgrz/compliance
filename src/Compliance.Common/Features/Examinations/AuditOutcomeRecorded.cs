using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Records the AuditOutcomeRecorded domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.examinations.audit-outcome-recorded", 1)]
public sealed record AuditOutcomeRecorded(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
