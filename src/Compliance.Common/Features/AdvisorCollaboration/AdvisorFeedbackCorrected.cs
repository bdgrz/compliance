using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Records the AdvisorFeedbackCorrected domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.advisorcollaboration.advisor-feedback-corrected", 1)]
public sealed record AdvisorFeedbackCorrected(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
