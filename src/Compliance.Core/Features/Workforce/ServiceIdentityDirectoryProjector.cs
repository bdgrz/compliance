using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public sealed partial class ServiceIdentityDirectoryProjector(
    IServiceIdentityDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("service-identities"),
            "ServiceIdentityDirectoryV1"),
      IProjectorHandler<ServiceIdentityRecorded>, IProjectorHandler<ServiceIdentityRevised>
{
    public ValueTask HandleAsync(ServiceIdentityRecorded ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ServiceIdentityRevised ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
