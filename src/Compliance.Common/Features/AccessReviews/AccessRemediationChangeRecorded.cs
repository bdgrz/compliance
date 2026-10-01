using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>A provider-side change was reported.</summary>
[Discriminator("bdgrz.access_review.remediation.change_recorded", 1)]
public sealed record AccessRemediationChangeRecorded(Uuid TenantId,
    Uuid CampaignId, long Revision, Uuid ItemId, AccessRemediationChangeView Change) : DomainEvent;
