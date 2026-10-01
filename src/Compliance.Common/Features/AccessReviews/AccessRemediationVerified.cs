using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>A later accepted population verified the remediation.</summary>
[Discriminator("bdgrz.access_review.remediation.verified", 1)]
public sealed record AccessRemediationVerified(Uuid TenantId,
    Uuid CampaignId, long Revision, Uuid ItemId, AccessRemediationVerificationView Verification) : DomainEvent;
