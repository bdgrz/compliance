using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed partial class RoleDirectoryProjector(IRoleDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(), "RoleDirectory"),
      IProjectorHandler<RoleDefined>,
      IProjectorHandler<RoleDeleted>
{
    public ValueTask HandleAsync(RoleDefined ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(RoleDeleted ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);
}
