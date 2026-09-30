namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>Explains why a non-human identity is unowned work (M0-D06).</summary>
public static class ServiceIdentityAccountability
{
    public static IReadOnlyList<string> Evaluate(string lifecycleStatus, DateOnly reviewBy,
        DateOnly today, string ownerKind, IReadOnlyCollection<string> ownerRelationshipStatuses,
        bool ownerTeamActive)
    {
        ArgumentNullException.ThrowIfNull(ownerRelationshipStatuses);
        if (lifecycleStatus == "retired")
            return [];
        var reasons = new List<string>();
        if (ownerKind == "person")
        {
            if (ownerRelationshipStatuses.Count == 0)
                reasons.Add("owner_not_on_roster");
            else if (ownerRelationshipStatuses.All(static status => status == "ended"))
                reasons.Add("owner_relationship_ended");
        }
        else if (!ownerTeamActive)
            reasons.Add("owner_team_deleted");
        if (reviewBy < today)
            reasons.Add("review_expired");
        return reasons;
    }

    /// <summary>A non-retired identity is expired from its expiry date onward.</summary>
    public static bool IsExpired(string lifecycleStatus, DateOnly? expiresOn, DateOnly today) =>
        lifecycleStatus != "retired" && expiresOn is { } expiry && expiry <= today;
}
