using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed class CurrentRatifiedIndependenceRulesReader(IAggregateReader reader)
{
    public async ValueTask<IndependenceRuleVersionView?> ReadActiveAsync(CancellationToken ct)
    {
        var rules = await reader.HydrateAsync(new IndependenceRuleCatalog(), ct).ConfigureAwait(false);
        var ratifications = await reader.HydrateAsync(new IndependenceRuleRatificationCatalog(), ct)
            .ConfigureAwait(false);
        if (ratifications.Active is not { } active)
            return null;
        var draft = rules.Versions.SingleOrDefault(item => item.Version == active.RuleVersion);
        if (draft is null)
            return null;
        var projected = IndependenceRuleVersionProjection.ApplyRatification(draft, active);
        return projected.IsRatified ? projected : null;
    }
}
