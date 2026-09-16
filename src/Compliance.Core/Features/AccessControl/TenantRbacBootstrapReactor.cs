using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed partial class TenantRbacBootstrapReactor(
    IProjectionCheckpointStore checkpoints,
    IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForPattern("bdgrz", "tenants")),
      IReactorHandler<TenantRegistered>
{
    public async ValueTask HandleAsync(IReactorContext<TenantRegistered> context, CancellationToken ct)
    {
        var tenantId = context.Trigger.TenantId;
        var memberId = RbacIds.Member(tenantId, context.Trigger.OwnerUserId);
        IRequest[] commands =
        [
            new RegisterMember(tenantId, context.Trigger.OwnerUserId),
            new DefineTeam(tenantId, BuiltInRbac.AdministratorsTeamId(tenantId),
                BuiltInRbac.AdministratorsTeamName),
            new DefineTeam(tenantId, BuiltInRbac.PowerUsersTeamId(tenantId), BuiltInRbac.PowerUsersTeamName),
            new DefineTeam(tenantId, BuiltInRbac.StandardUsersTeamId(tenantId), BuiltInRbac.StandardUsersTeamName),
            new DefineRole(tenantId, BuiltInRbac.TenantAdministrationRoleId(tenantId),
                BuiltInRbac.TenantAdministrationRoleName),
            new DefineRole(tenantId, BuiltInRbac.ComplianceManagementRoleId(tenantId),
                BuiltInRbac.ComplianceManagementRoleName),
            new DefineRole(tenantId, BuiltInRbac.ComplianceParticipationRoleId(tenantId),
                BuiltInRbac.ComplianceParticipationRoleName),
            new AssignTeamRole(tenantId, BuiltInRbac.AdministratorsTeamId(tenantId),
                BuiltInRbac.TenantAdministrationRoleId(tenantId)),
            new AssignTeamRole(tenantId, BuiltInRbac.PowerUsersTeamId(tenantId),
                BuiltInRbac.ComplianceManagementRoleId(tenantId)),
            new AssignTeamRole(tenantId, BuiltInRbac.StandardUsersTeamId(tenantId),
                BuiltInRbac.ComplianceParticipationRoleId(tenantId)),
            new AssignRolePermission(tenantId, BuiltInRbac.TenantAdministrationRoleId(tenantId),
                RbacPermissions.TenantAccess),
            new AssignRolePermission(tenantId, BuiltInRbac.TenantAdministrationRoleId(tenantId),
                RbacPermissions.TenantRbacManage),
            new AssignRolePermission(tenantId, BuiltInRbac.ComplianceManagementRoleId(tenantId),
                RbacPermissions.TenantAccess),
            new AssignRolePermission(tenantId, BuiltInRbac.ComplianceParticipationRoleId(tenantId),
                RbacPermissions.TenantAccess),
            new AssignTeamMember(tenantId, BuiltInRbac.AdministratorsTeamId(tenantId), memberId),
        ];

        foreach (var command in commands)
            await bus.SendReactionAsync(command, context, ct);
    }
}
