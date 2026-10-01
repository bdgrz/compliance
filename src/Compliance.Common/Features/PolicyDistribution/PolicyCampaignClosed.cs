using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

[Discriminator("bdgrz.policy_campaign.closed", 1)]
public sealed record PolicyCampaignClosed(Uuid TenantId, Uuid CampaignId, DateOnly ClosedOn,
    string Rationale, CampaignTotalsView Totals, ActorReference Actor, DateTimeOffset ClosedAt)
    : DomainEvent;
