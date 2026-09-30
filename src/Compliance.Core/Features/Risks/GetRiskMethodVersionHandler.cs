using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public sealed class GetRiskMethodVersionHandler(IAggregateReader reader)
    : IRequestHandler<GetRiskMethodVersion, RiskMethodVersionView>
{
    public async ValueTask<Result<RiskMethodVersionView>> HandleAsync(
        IRequestContext<GetRiskMethodVersion> context, CancellationToken ct)
    {
        var method = await reader.HydrateAsync(new RiskMethod(context.Request.TenantId,
            context.Request.ProgramId), ct).ConfigureAwait(false);
        return GetRiskMethodHandler.Found(method.GetVersion(context.Request.Version));
    }
}
