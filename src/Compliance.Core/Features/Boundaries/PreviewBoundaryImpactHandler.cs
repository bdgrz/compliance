using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public sealed class PreviewBoundaryImpactHandler(BoundaryImpactService service)
    : IRequestHandler<PreviewBoundaryImpact, BoundaryImpactPreview>
{
    public ValueTask<Result<BoundaryImpactPreview>> HandleAsync(
        IRequestContext<PreviewBoundaryImpact> context, CancellationToken ct) =>
        service.PreviewAsync(context.Request, ct);
}
