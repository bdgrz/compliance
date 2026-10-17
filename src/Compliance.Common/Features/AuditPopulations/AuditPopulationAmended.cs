using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Records the AuditPopulationAmended domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.auditpopulations.audit-population-amended", 1)]
public sealed record AuditPopulationAmended(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
