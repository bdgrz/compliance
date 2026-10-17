using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Records the TypeIBaselineFrozen domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.examinations.type-i-baseline-frozen", 1)]
public sealed record TypeIBaselineFrozen(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
