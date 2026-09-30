using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed partial class ApplicationDirectoryProjector(IApplicationDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(), "ApplicationDirectoryV2"),
      IProjectorHandler<ApplicationDeclared>, IProjectorHandler<ApplicationRevised>,
      IProjectorHandler<SystemInstanceDeclared>, IProjectorHandler<SystemInstanceRegistered>,
      IProjectorHandler<ApplicationRetired>, IProjectorHandler<SystemInstanceRetired>
{
    public ValueTask HandleAsync(ApplicationDeclared ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ApplicationRevised ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(SystemInstanceDeclared ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ApplicationRetired ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(SystemInstanceRetired ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(SystemInstanceRegistered ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
