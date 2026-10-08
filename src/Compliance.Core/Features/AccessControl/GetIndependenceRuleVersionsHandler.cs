using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class GetIndependenceRuleVersionsHandler(IAggregateReader reader)
    : IRequestHandler<GetIndependenceRuleVersions, IReadOnlyList<IndependenceRuleVersionView>>
{
    public async ValueTask<Result<IReadOnlyList<IndependenceRuleVersionView>>> HandleAsync(
        IRequestContext<GetIndependenceRuleVersions> context, CancellationToken ct)
    {
        var catalog = await reader.HydrateAsync(new IndependenceRuleCatalog(), ct).ConfigureAwait(false);
        return Result<IReadOnlyList<IndependenceRuleVersionView>>.Success(catalog.Versions);
    }
}
