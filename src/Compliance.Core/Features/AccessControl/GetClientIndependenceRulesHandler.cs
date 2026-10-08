using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class GetClientIndependenceRulesHandler(IAggregateReader reader)
    : IRequestHandler<GetClientIndependenceRules, IReadOnlyList<IndependenceRuleVersionView>>
{
    public async ValueTask<Result<IReadOnlyList<IndependenceRuleVersionView>>> HandleAsync(
        IRequestContext<GetClientIndependenceRules> context, CancellationToken ct)
    {
        var catalog = await reader.HydrateAsync(new IndependenceRuleCatalog(), ct).ConfigureAwait(false);
        return Result<IReadOnlyList<IndependenceRuleVersionView>>.Success(catalog.Versions);
    }
}
