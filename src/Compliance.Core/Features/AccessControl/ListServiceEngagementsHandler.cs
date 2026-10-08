using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class ListServiceEngagementsHandler(IAggregateReader reader)
    : IRequestHandler<ListServiceEngagements, IReadOnlyList<ServiceEngagementView>>
{
    public async ValueTask<Result<IReadOnlyList<ServiceEngagementView>>> HandleAsync(IRequestContext<ListServiceEngagements> context, CancellationToken ct)
    {
        var ledger = await reader.HydrateAsync(new IndependenceLedger(context.Request.TenantId), ct).ConfigureAwait(false);
        return Result<IReadOnlyList<ServiceEngagementView>>.Success(ledger.Engagements);
    }
}
