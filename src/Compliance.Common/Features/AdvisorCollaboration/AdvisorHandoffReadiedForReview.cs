using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Records the AdvisorHandoffReadiedForReview domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.advisorcollaboration.advisor-handoff-readied-for-review", 1)]
public sealed record AdvisorHandoffReadiedForReview(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
