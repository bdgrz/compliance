using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed partial class PermissionProjector(IPermissionProjection projection)
    : Projector(projection, EventStreamPattern.ForPattern("tenant", area: null), "PermissionProjection"),
      IProjectorHandler<MemberRegistered>,
      IProjectorHandler<TeamDefined>,
      IProjectorHandler<TeamDeleted>,
      IProjectorHandler<TeamMemberAssigned>,
      IProjectorHandler<RoleDefined>,
      IProjectorHandler<RoleDeleted>,
      IProjectorHandler<RolePermissionAssigned>,
      IProjectorHandler<TeamRoleAssigned>
{
    public ValueTask HandleAsync(MemberRegistered ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(TeamDefined ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(TeamDeleted ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(TeamMemberAssigned ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(RoleDefined ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(RoleDeleted ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(RolePermissionAssigned ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(TeamRoleAssigned ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);
}
