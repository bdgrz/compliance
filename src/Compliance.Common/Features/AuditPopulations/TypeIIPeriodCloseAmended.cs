using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Records the TypeIIPeriodCloseAmended domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.auditpopulations.type-i-i-period-close-amended", 1)]
public sealed record TypeIIPeriodCloseAmended(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
