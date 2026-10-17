using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Records the ManagementReviewRecorded domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.operations.management-review-recorded", 1)]
public sealed record ManagementReviewRecorded(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
