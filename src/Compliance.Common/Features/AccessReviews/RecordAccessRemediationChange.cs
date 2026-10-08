using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Records a provider-side change for a modify or revoke decision; it does not verify remediation.</summary>
[Discriminator("bdgrz.access_review.remediation.change.record", 1)]
public sealed record RecordAccessRemediationChange(Uuid TenantId, Uuid CampaignId,
    Uuid ItemId, long ExpectedRevision, string Reference, string Description,
    DateTimeOffset ChangedAt)
    : IRequest<AccessRemediationChangeView>, IAccessReviewMutationRequest, ICallable;
