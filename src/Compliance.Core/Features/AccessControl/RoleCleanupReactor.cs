using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Removes a deleted role's permission assignments and team assignments so no orphaned
///     <see cref="RolePermission" />/<see cref="TeamRole" /> assignment survives the role itself.
///     Not authoritative on its own — see <see cref="TeamCleanupReactor" /> for the same reasoning.
/// </summary>
public sealed partial class RoleCleanupReactor(
    IProjectionCheckpointStore checkpoints,
    IRequestBus bus,
    IRolePermissionDirectoryReader permissions,
    IRoleTeamDirectoryReader teams)
    : Reactor(checkpoints, EventStreamPattern.ForTenant("rbac-roles"), "RoleCleanup"),
      IReactorHandler<RoleDeleted>
{
    public async ValueTask HandleAsync(IReactorContext<RoleDeleted> context, CancellationToken ct)
    {
        var tenantId = context.Trigger.TenantId;
        var roleId = context.Trigger.RoleId;

        string? permissionCursor = null;
        do
        {
            var page = await permissions.ListAsync(tenantId, roleId, 200, permissionCursor, null, descending: false, ct)
                .ConfigureAwait(false);
            foreach (var permission in page.Items)
            {
                await bus.SendReactionAsync(
                        new RemoveRolePermission(tenantId, roleId, permission.Permission), context, ct)
                    .ConfigureAwait(false);
            }

            permissionCursor = page.NextCursor;
        } while (permissionCursor is not null);

        string? teamCursor = null;
        do
        {
            var page = await teams.ListAsync(tenantId, roleId, 200, teamCursor, null, descending: false, ct)
                .ConfigureAwait(false);
            foreach (var team in page.Items)
            {
                await bus.SendReactionAsync(new RemoveTeamRole(tenantId, team.TeamId, roleId), context, ct)
                    .ConfigureAwait(false);
            }

            teamCursor = page.NextCursor;
        } while (teamCursor is not null);
    }
}
