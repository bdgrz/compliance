using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     Launches a campaign bound to exact material and a frozen roster snapshot. The audience
///     itself follows in bounded <see cref="PolicyCampaignAudienceFrozen" /> batches.
/// </summary>
[Discriminator("bdgrz.policy_campaign.launched", 1)]
public sealed record PolicyCampaignLaunched(Uuid TenantId, Uuid ProgramId, Uuid CampaignId,
    CampaignSubject Subject, string AudienceKind, IReadOnlyList<string> AudienceTeams,
    Uuid RosterSnapshotId, string RosterContentSha256, DateOnly LaunchedOn, DateOnly DueOn,
    string? Instructions, string? AcknowledgementText, int AudienceCount,
    ActorReference Actor, Uuid ActorMemberId, DateTimeOffset LaunchedAt) : DomainEvent;
