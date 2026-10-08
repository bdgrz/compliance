using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class GetEngagementManagementAcknowledgementsHandler(IAggregateReader reader)
    : IRequestHandler<GetEngagementManagementAcknowledgements, IReadOnlyList<EngagementManagementAcknowledgementView>>
{
    public async ValueTask<Result<IReadOnlyList<EngagementManagementAcknowledgementView>>> HandleAsync(
        IRequestContext<GetEngagementManagementAcknowledgements> context, CancellationToken ct)
    {
        var ledger = await reader.HydrateAsync(new IndependenceLedger(context.Request.TenantId), ct).ConfigureAwait(false);
        return Result<IReadOnlyList<EngagementManagementAcknowledgementView>>.Success(
            ledger.ManagementAcknowledgements(context.Request.EngagementId));
    }
}
