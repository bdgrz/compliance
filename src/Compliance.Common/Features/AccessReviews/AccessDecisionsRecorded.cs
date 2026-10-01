using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>One or more attributable reviewer decisions were recorded.</summary>
[Discriminator("bdgrz.access_review.decisions.recorded", 1)]
public sealed record AccessDecisionsRecorded(Uuid TenantId,
    Uuid CampaignId, long Revision, IReadOnlyList<AccessDecisionView> Decisions) : DomainEvent;
