using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

public sealed partial class ProviderProjector(IProviderProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(ProviderRegister.Area), FitzProviderDirectory.ProjectorName),
      IProjectorHandler<ProviderRecorded>, IProjectorHandler<ProviderRevised>
{
    public ValueTask HandleAsync(ProviderRecorded ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ProviderRevised ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);
}
