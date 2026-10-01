using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>A bounded exception that counts the person as excepted until it expires.</summary>
[Discriminator("bdgrz.policy_campaign.waiver.approved", 1)]
public sealed record CampaignWaiverApproved(Uuid TenantId, Uuid CampaignId,
    Uuid WaiverId, Uuid PersonId, string Reason, DateOnly ExpiresOn,
    ActorReference Approver, Uuid ApproverMemberId, DateTimeOffset ApprovedAt) : DomainEvent;
