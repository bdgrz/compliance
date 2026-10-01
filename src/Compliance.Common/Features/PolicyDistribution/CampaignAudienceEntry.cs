using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>A workforce person frozen into a campaign's launch audience.</summary>
public sealed record CampaignAudienceEntry(Uuid PersonId, string DisplayName,
    string WorkerType, string? Department, DateOnly StartDate, DateOnly DueOn);
