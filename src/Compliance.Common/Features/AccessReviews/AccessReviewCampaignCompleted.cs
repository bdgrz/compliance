using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>The campaign was completed with its final immutable snapshot.</summary>
[Discriminator("bdgrz.access_review.campaign.completed", 1)]
public sealed record AccessReviewCampaignCompleted(Uuid TenantId,
    Uuid CampaignId, long Revision, AccessReviewCampaignCompletionView Completion) : DomainEvent;
