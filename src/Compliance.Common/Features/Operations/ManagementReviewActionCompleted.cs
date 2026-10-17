using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Records the ManagementReviewActionCompleted domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.operations.management-review-action-completed", 1)]
public sealed record ManagementReviewActionCompleted(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
