using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Verifies remediation only when a later accepted population shows the access removed or changed.</summary>
[Discriminator("bdgrz.access_review.remediation.verify", 1)]
public sealed record VerifyAccessRemediation(Uuid TenantId, Uuid CampaignId,
    Uuid ItemId, long ExpectedRevision, Uuid PopulationId)
    : IRequest<AccessRemediationVerificationView>, IAccessReviewRequest, ICallable;
