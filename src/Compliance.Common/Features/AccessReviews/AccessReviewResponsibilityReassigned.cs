using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>A campaign responsibility was changed after its launch-frozen owner lost eligibility.</summary>
[Discriminator("bdgrz.access_review.responsibility.reassigned", 1)]
public sealed record AccessReviewResponsibilityReassigned(Uuid TenantId, Uuid CampaignId,
    long Revision, AccessReviewResponsibilityReassignmentView Reassignment) : DomainEvent;
