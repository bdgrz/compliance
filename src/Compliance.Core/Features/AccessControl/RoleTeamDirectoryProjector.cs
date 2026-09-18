using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed partial class RoleTeamDirectoryProjector(IRoleTeamDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(), "RoleTeamDirectory"),
      IProjectorHandler<TeamRoleAssigned>,
      IProjectorHandler<TeamRoleRemoved>
{
    public ValueTask HandleAsync(TeamRoleAssigned ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(TeamRoleRemoved ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);
}
