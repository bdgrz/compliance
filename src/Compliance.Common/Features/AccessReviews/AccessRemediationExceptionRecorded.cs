using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>An exception to verified remediation was approved.</summary>
[Discriminator("bdgrz.access_review.remediation.exception_recorded", 1)]
public sealed record AccessRemediationExceptionRecorded(Uuid TenantId,
    Uuid CampaignId, long Revision, Uuid ItemId, AccessRemediationExceptionView Exception) : DomainEvent;
