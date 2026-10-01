using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>A workforce person whose frozen roster facts match a campaign audience rule.</summary>
public sealed record RosterMatch(Uuid PersonId, string DisplayName, string WorkerType,
    string? Department, DateOnly StartDate);
