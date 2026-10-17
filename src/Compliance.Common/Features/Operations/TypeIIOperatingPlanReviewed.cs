using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Records the TypeIIOperatingPlanReviewed domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.operations.type-i-i-operating-plan-reviewed", 1)]
public sealed record TypeIIOperatingPlanReviewed(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
