using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed partial class ApplicationDirectoryProjector(IApplicationDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("applications"), "ApplicationDirectory"),
      IProjectorHandler<ApplicationDeclared>, IProjectorHandler<ApplicationRevised>,
      IProjectorHandler<SystemInstanceDeclared>
{
    public ValueTask HandleAsync(ApplicationDeclared ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ApplicationRevised ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(SystemInstanceDeclared ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
