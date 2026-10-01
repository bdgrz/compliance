using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>A campaign evaluated as of a date. <c>Status</c> is <c>open</c> or <c>closed</c>.</summary>
public sealed record CampaignView(Uuid TenantId, Uuid ProgramId, Uuid CampaignId,
    CampaignSubject Subject, string AudienceKind, IReadOnlyList<string> AudienceTeams,
    Uuid RosterSnapshotId, string RosterContentSha256, Uuid LatestRosterSnapshotId,
    DateOnly LaunchedOn, DateOnly DueOn, string? Instructions, string? AcknowledgementText,
    string Status, DateOnly AsOf, CampaignTotalsView Totals, ActorReference LaunchedBy,
    DateTimeOffset LaunchedAt, ActorReference? ClosedBy, DateTimeOffset? ClosedAt,
    CampaignTotalsView? ClosingTotals);
