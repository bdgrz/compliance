using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Records the AuditPopulationDefined domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.auditpopulations.audit-population-defined", 1)]
public sealed record AuditPopulationDefined(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
