using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Records the TypeIBaselineAmended domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.examinations.type-i-baseline-amended", 1)]
public sealed record TypeIBaselineAmended(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
