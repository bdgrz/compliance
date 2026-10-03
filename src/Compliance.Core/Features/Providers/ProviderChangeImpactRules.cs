namespace Bdgrz.Compliance.Features.Providers;

static class ProviderChangeImpactRules
{
    public static bool IsAffectedByChange(ProviderDependency dependency, string changeKind,
        DateOnly effectiveOn)
    {
        var date = new DateTimeOffset(effectiveOn.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (changeKind == "termination")
            return dependency.EffectiveFrom < date &&
                   (dependency.EffectiveUntilExclusive is null ||
                    date < dependency.EffectiveUntilExclusive);
        return dependency.EffectiveFrom <= date &&
               (dependency.EffectiveUntilExclusive is null || date < dependency.EffectiveUntilExclusive);
    }

    public static string DependencyRelationship(string changeKind, bool affected) =>
        changeKind == "termination"
            ? affected ? "provider_dependency_active_before_termination" :
                "provider_dependency_outside_termination_window"
            : affected ? "provider_dependency_effective_on_change_date" :
                "provider_dependency_outside_change_date";
}
