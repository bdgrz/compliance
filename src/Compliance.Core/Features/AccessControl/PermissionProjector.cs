using System.Collections.Frozen;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed partial class PermissionProjector(IPermissionProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(), "PermissionProjection"),
      IProjectorHandler<MemberRegistered>,
      IProjectorHandler<TeamDefined>,
      IProjectorHandler<TeamDeleted>,
      IProjectorHandler<TeamMemberAssigned>,
      IProjectorHandler<TeamMemberRemoved>,
      IProjectorHandler<RoleDefined>,
      IProjectorHandler<RoleDeleted>,
      IProjectorHandler<RolePermissionAssigned>,
      IProjectorHandler<RolePermissionRemoved>,
      IProjectorHandler<TeamRoleAssigned>,
      IProjectorHandler<TeamRoleRemoved>
{
    static readonly FrozenSet<Type> HandledEventTypes = typeof(PermissionProjector).GetInterfaces()
        .Where(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IProjectorHandler<>))
        .Select(type => type.GetGenericArguments()[0])
        .ToFrozenSet();

    public static bool Handles(DomainEvent domainEvent) => HandledEventTypes.Contains(domainEvent.GetType());

    public ValueTask HandleAsync(MemberRegistered ev, IProjectorContext context, CancellationToken ct) =>
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
