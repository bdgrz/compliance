using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Records the AuditPopulationFrozen domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.auditpopulations.audit-population-frozen", 1)]
public sealed record AuditPopulationFrozen(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
