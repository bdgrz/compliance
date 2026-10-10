namespace Bdgrz.Compliance.Features.AccessControl;

static class IndependenceRuleVersionProjection
{
    public static IReadOnlyList<IndependenceRuleVersionView> ApplyRatifications(
        IReadOnlyList<IndependenceRuleVersionView> versions,
        IReadOnlyList<IndependenceRuleRatificationView> ratifications)
    {
        ArgumentNullException.ThrowIfNull(versions);
        ArgumentNullException.ThrowIfNull(ratifications);
        return Array.AsReadOnly(versions.Select(version => ApplyRatification(version,
            ratifications.SingleOrDefault(item => item.RuleVersion == version.Version))).ToArray());
    }

    public static IndependenceRuleVersionView ApplyRatification(IndependenceRuleVersionView version,
        IndependenceRuleRatificationView? ratification)
    {
        if (ratification is null || ratification.RuleContentDigest != IndependenceSourceDigest.RuleContent(version.Content))
            return version with { IsRatified = false, Ratification = null };
        return version with { IsRatified = true, Ratification = ratification };
    }
}
