using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Records the AdvisorFeedbackRecorded domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.advisorcollaboration.advisor-feedback-recorded", 1)]
public sealed record AdvisorFeedbackRecorded(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
