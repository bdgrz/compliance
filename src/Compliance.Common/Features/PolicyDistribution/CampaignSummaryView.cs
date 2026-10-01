using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>The projected list row of a campaign.</summary>
public sealed record CampaignSummaryView(Uuid TenantId, Uuid ProgramId, Uuid CampaignId,
    string SubjectKind, Uuid SubjectId, string SubjectIdentifier, long SubjectVersion,
    string Title, DateOnly DueOn, string Status, int LaunchAudience, DateTimeOffset LaunchedAt);
