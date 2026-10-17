using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Records the ObservationPeriodCloseFrozen domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.auditpopulations.observation-period-close-frozen", 1)]
public sealed record ObservationPeriodCloseFrozen(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
