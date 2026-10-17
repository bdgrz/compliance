using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Records the ObservationPeriodCloseProposed domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.auditpopulations.observation-period-close-proposed", 1)]
public sealed record ObservationPeriodCloseProposed(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
