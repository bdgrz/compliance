using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     One bounded batch of a reconciliation against a later roster snapshot. The frozen launch
///     audience is never edited; a reconciliation with no changes still records its snapshot.
/// </summary>
[Discriminator("bdgrz.policy_campaign.audience.amended", 1)]
public sealed record PolicyCampaignAudienceAmended(Uuid TenantId, Uuid CampaignId,
    long Reconciliation, int Batch, Uuid RosterSnapshotId, string RosterContentSha256,
    DateOnly ObservedOn, IReadOnlyList<CampaignAudienceAmendment> Amendments,
    ActorReference Actor, DateTimeOffset AmendedAt) : DomainEvent;
