using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class GetServiceEngagementAcceptanceHistoryHandler(IAggregateReader reader)
    : IRequestHandler<GetServiceEngagementAcceptanceHistory, IReadOnlyList<ServiceEngagementAcceptanceView>>
{
    public async ValueTask<Result<IReadOnlyList<ServiceEngagementAcceptanceView>>> HandleAsync(
        IRequestContext<GetServiceEngagementAcceptanceHistory> context, CancellationToken ct)
    {
        var ledger = await reader.HydrateAsync(new IndependenceLedger(context.Request.TenantId), ct).ConfigureAwait(false);
        return Result<IReadOnlyList<ServiceEngagementAcceptanceView>>.Success(ledger.AcceptanceHistory(context.Request.EngagementId));
    }
}
