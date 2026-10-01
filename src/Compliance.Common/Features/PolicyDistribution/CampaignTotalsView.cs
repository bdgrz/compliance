namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     Reconciling totals: <c>Population</c> = <c>LaunchAudience</c> + <c>Added</c>, and
///     <c>Population</c> = <c>Pending</c> + <c>Overdue</c> + <c>Satisfied</c> + <c>Excepted</c> +
///     <c>Removed</c>.
/// </summary>
public sealed record CampaignTotalsView(int Population, int LaunchAudience, int Added,
    int Removed, int Pending, int Overdue, int Satisfied, int Excepted);
