using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Records an audited source-level replacement for an ineligible campaign responsibility.</summary>
[Discriminator("bdgrz.access_review.responsibility.reassign", 1)]
public sealed record ReassignAccessReviewResponsibility(Uuid TenantId, Uuid CampaignId,
    Uuid ItemId, string Responsibility, long ExpectedRevision, Uuid AssignedMemberId,
    string Reason, string? DelegationReason = null) : IRequest<AccessReviewResponsibilityReassignmentView>,
    IAccessReviewMutationRequest, ICallable;
