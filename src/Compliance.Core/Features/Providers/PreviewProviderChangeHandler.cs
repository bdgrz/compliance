using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

public sealed class PreviewProviderChangeHandler(ProviderChangeImpactService impact)
    : IRequestHandler<PreviewProviderChange, ProviderChangeImpactPreview>
{
    public ValueTask<Result<ProviderChangeImpactPreview>> HandleAsync(
        IRequestContext<PreviewProviderChange> context, CancellationToken ct) =>
        impact.PreviewAsync(context.Request, ct);
}
