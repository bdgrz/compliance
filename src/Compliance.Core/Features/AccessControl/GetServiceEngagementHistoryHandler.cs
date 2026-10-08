using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class GetServiceEngagementHistoryHandler(IAggregateReader reader)
    : IRequestHandler<GetServiceEngagementHistory, IReadOnlyList<ServiceEngagementView>>
{
    public async ValueTask<Result<IReadOnlyList<ServiceEngagementView>>> HandleAsync(IRequestContext<GetServiceEngagementHistory> context, CancellationToken ct)
    {
        var ledger = await reader.HydrateAsync(new IndependenceLedger(context.Request.TenantId), ct).ConfigureAwait(false);
        if (ledger.Engagement(context.Request.EngagementId) is null)
            return Result<IReadOnlyList<ServiceEngagementView>>.Failure(new RequestError(RequestErrorKind.NotFound, "The client service engagement was not found."));
        return Result<IReadOnlyList<ServiceEngagementView>>.Success(ledger.EngagementHistory(context.Request.EngagementId));
    }
}
