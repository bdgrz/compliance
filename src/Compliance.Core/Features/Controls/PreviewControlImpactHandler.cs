using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public sealed class PreviewControlImpactHandler(ControlImpactService service)
    : IRequestHandler<PreviewControlImpact, ControlImpactPreview>
{
    public ValueTask<Result<ControlImpactPreview>> HandleAsync(
        IRequestContext<PreviewControlImpact> context, CancellationToken ct) =>
        service.PreviewAsync(context.Request, ct);
}
