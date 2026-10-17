using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Records the AuditPopulationGenerated domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.auditpopulations.audit-population-generated", 1)]
public sealed record AuditPopulationGenerated(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
