using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class GetServiceEngagementHandler(IAggregateReader reader)
    : IRequestHandler<GetServiceEngagement, ServiceEngagementView>
{
    public async ValueTask<Result<ServiceEngagementView>> HandleAsync(IRequestContext<GetServiceEngagement> context, CancellationToken ct)
    {
        var ledger = await reader.HydrateAsync(new IndependenceLedger(context.Request.TenantId), ct).ConfigureAwait(false);
        if (ledger.Engagement(context.Request.EngagementId) is null)
            return Result<ServiceEngagementView>.Failure(new RequestError(RequestErrorKind.NotFound, "The client service engagement was not found."));
        return Result<ServiceEngagementView>.Success(ledger.Engagement(context.Request.EngagementId)!);
    }
}
