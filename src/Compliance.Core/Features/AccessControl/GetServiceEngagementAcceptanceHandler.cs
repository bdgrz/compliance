using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class GetServiceEngagementAcceptanceHandler(IAggregateReader reader)
    : IRequestHandler<GetServiceEngagementAcceptance, ServiceEngagementAcceptanceView>
{
    public async ValueTask<Result<ServiceEngagementAcceptanceView>> HandleAsync(
        IRequestContext<GetServiceEngagementAcceptance> context, CancellationToken ct)
    {
        var ledger = await reader.HydrateAsync(new IndependenceLedger(context.Request.TenantId), ct).ConfigureAwait(false);
        return ledger.Acceptance(context.Request.EngagementId) is { } accepted
            ? Result<ServiceEngagementAcceptanceView>.Success(accepted)
            : Result<ServiceEngagementAcceptanceView>.Failure(new RequestError(RequestErrorKind.NotFound, "The accepted engagement was not found."));
    }
}
