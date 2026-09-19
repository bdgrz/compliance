using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed partial class ClientServiceDirectoryProjector(IClientServiceDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(), "ClientServiceDirectory"),
      IProjectorHandler<ClientServiceCreated>, IProjectorHandler<ClientServiceRevised>,
      IProjectorHandler<ClientServiceRetired>
{
    public ValueTask HandleAsync(ClientServiceCreated ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ClientServiceRevised ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ClientServiceRetired ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);
}
