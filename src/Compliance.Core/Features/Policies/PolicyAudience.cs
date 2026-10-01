using Bdgrz.Compliance.Features.Workforce;

namespace Bdgrz.Compliance.Features.Policies;

/// <summary>
///     The M0-D12 audience rules. Audiences are sets of workforce people, never platform members,
///     and are never inferred from application access, platform teams, or the IdP.
/// </summary>
public static class PolicyAudience
{
    public const string CoreSecurity = "core_security";
    public const string RoleTargeted = "role_targeted";

    public static string? Validate(string? kind, IReadOnlyList<string>? teams)
    {
        var clean = CleanTeams(teams);
        return kind switch
        {
            CoreSecurity when clean.Count == 0 => null,
            CoreSecurity => "A core_security audience names no roster teams.",
            RoleTargeted when clean.Count is >= 1 and <= 50 &&
                              clean.All(static team => team.Length <= 200) => null,
            RoleTargeted => "A role_targeted audience names 1 to 50 roster teams of at most 200 characters.",
            _ => "The audience kind must be core_security or role_targeted.",
        };
    }

    public static IReadOnlyList<string> CleanTeams(IReadOnlyList<string>? teams) =>
        (teams ?? []).Where(static team => !string.IsNullOrWhiteSpace(team))
        .Select(static team => team.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    /// <summary>
    ///     Whether a frozen work relationship is in the audience. <c>core_security</c> covers every
    ///     current employee and contractor; <c>role_targeted</c> covers every current worker whose
    ///     roster team (department) is named.
    /// </summary>
    public static bool Matches(string kind, IReadOnlyList<string> teams,
        FrozenWorkRelationship relationship)
    {
        ArgumentNullException.ThrowIfNull(relationship);
        if (relationship.LifecycleStatus == "ended")
            return false;
        return kind == CoreSecurity
            ? relationship.WorkerType is "employee" or "contractor"
            : relationship.Department is { } department &&
              teams.Contains(department.Trim(), StringComparer.OrdinalIgnoreCase);
    }
}
