using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Records the TypeIIPlanApproved domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.examinations.type-i-i-plan-approved", 1)]
public sealed record TypeIIPlanApproved(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
