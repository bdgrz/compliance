using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Records the GovernanceReviewScheduled domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.operations.governance-review-scheduled", 1)]
public sealed record GovernanceReviewScheduled(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
