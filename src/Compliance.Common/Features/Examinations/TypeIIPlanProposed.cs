using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Records the TypeIIPlanProposed domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.examinations.type-i-i-plan-proposed", 1)]
public sealed record TypeIIPlanProposed(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
