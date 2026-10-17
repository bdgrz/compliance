using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Records the AuditPopulationRowClassified domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.auditpopulations.audit-population-row-classified", 1)]
public sealed record AuditPopulationRowClassified(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
