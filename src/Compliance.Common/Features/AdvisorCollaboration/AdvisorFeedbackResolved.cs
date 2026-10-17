using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Records the AdvisorFeedbackResolved domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.advisorcollaboration.advisor-feedback-resolved", 1)]
public sealed record AdvisorFeedbackResolved(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
