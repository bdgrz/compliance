using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Records the TypeIIOperatingPlanApproved domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.operations.type-i-i-operating-plan-approved", 1)]
public sealed record TypeIIOperatingPlanApproved(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
