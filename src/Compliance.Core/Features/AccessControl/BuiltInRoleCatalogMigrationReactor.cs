using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Replays tenant registrations to migrate existing tenants to the accepted built-in role catalog.</summary>
public sealed partial class BuiltInRoleCatalogMigrationReactor(
    IProjectionCheckpointStore checkpoints,
    IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForPattern("bdgrz", "tenants"), "BuiltInRoleCatalogV1"),
      IReactorHandler<TenantRegistered>
{
    public async ValueTask HandleAsync(IReactorContext<TenantRegistered> context, CancellationToken ct)
    {
        var tenantId = context.Trigger.TenantId;
        IRequest[] commands =
        [
            new RenameRole(tenantId, BuiltInRbac.TenantAdministrationRoleId(tenantId),
                BuiltInRbac.TenantAdministrationRoleName),
            new RenameRole(tenantId, BuiltInRbac.ComplianceManagementRoleId(tenantId),
                BuiltInRbac.ComplianceManagementRoleName),
            new RenameRole(tenantId, BuiltInRbac.ComplianceParticipationRoleId(tenantId),
                BuiltInRbac.ComplianceParticipationRoleName),
            new DefineTeam(tenantId, BuiltInRbac.ViewersTeamId(tenantId), BuiltInRbac.ViewersTeamName),
            new DefineRole(tenantId, BuiltInRbac.ViewerRoleId(tenantId), BuiltInRbac.ViewerRoleName),
            new AssignTeamRole(tenantId, BuiltInRbac.ViewersTeamId(tenantId), BuiltInRbac.ViewerRoleId(tenantId)),
            new AssignRolePermission(tenantId, BuiltInRbac.ViewerRoleId(tenantId), RbacPermissions.TenantAccess),
        ];

        foreach (var command in commands)
            await bus.SendReactionAsync(command, context, ct);
    }
}
