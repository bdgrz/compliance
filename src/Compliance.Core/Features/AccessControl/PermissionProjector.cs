using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed partial class PermissionProjector(IPermissionProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(), "PermissionProjection"),
      IProjectorHandler<MemberRegistered>,
      IProjectorHandler<MemberSuspended>,
      IProjectorHandler<MemberReinstated>,
      IProjectorHandler<MemberDeprovisioned>,
      IProjectorHandler<TeamDefined>,
      IProjectorHandler<TeamDeleted>,
      IProjectorHandler<TeamMemberAssigned>,
      IProjectorHandler<TeamMemberRemoved>,
      IProjectorHandler<RoleDefined>,
      IProjectorHandler<RoleRenamed>,
      IProjectorHandler<RoleDeleted>,
      IProjectorHandler<RolePermissionAssigned>,
      IProjectorHandler<RolePermissionRemoved>,
      IProjectorHandler<TeamRoleAssigned>,
      IProjectorHandler<TeamRoleRemoved>
{
    public static bool RevokesAllMemberAccess(DomainEvent domainEvent, Uuid memberId) => domainEvent switch
    {
        MemberRegistered registered => registered.MemberId == memberId &&
            !string.Equals(registered.Affiliation, "client_personnel", StringComparison.Ordinal),
        MemberSuspended suspended => suspended.MemberId == memberId,
        MemberDeprovisioned deprovisioned => deprovisioned.MemberId == memberId,
        _ => false,
    };

    public static bool RevokesAccessPath(DomainEvent domainEvent, Uuid memberId,
        string permission, MemberAccessEdge currentAccess) => domainEvent switch
        {
            TeamMemberRemoved removed => removed.MemberId == memberId &&
                removed.TeamId == currentAccess.TeamId,
            TeamDeleted deleted => currentAccess.TeamId == deleted.TeamId,
            TeamRoleRemoved removed => currentAccess.TeamId == removed.TeamId &&
                currentAccess.RoleId == removed.RoleId,
            RoleDeleted deleted => currentAccess.RoleId == deleted.RoleId,
            RolePermissionRemoved removed => currentAccess.RoleId == removed.RoleId &&
                currentAccess.Permissions.Contains(permission, StringComparer.Ordinal),
            _ => false,
        };

    public ValueTask HandleAsync(MemberRegistered ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(MemberSuspended ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(MemberReinstated ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(MemberDeprovisioned ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(TeamDefined ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(TeamDeleted ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(TeamMemberAssigned ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(TeamMemberRemoved ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(RoleDefined ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(RoleRenamed ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(RoleDeleted ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(RolePermissionAssigned ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(RolePermissionRemoved ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(TeamRoleAssigned ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(TeamRoleRemoved ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);
}
