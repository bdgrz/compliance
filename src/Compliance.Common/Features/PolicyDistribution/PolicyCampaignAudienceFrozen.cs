using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

[Discriminator("bdgrz.policy_campaign.audience.frozen", 1)]
public sealed record PolicyCampaignAudienceFrozen(Uuid TenantId, Uuid CampaignId, int Batch,
    IReadOnlyList<CampaignAudienceEntry> Entries) : DomainEvent;
