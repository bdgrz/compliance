using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
/// Replays tenant registrations to grant the workforce roster permission (M0-D06 defaults).
/// </summary>
public sealed partial class WorkforceGrantBackfillReactor(
    IProjectionCheckpointStore checkpoints,
    IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForPattern("bdgrz", "tenants")),
      IReactorHandler<TenantRegistered>
{
    public async ValueTask HandleAsync(IReactorContext<TenantRegistered> context,
        CancellationToken ct)
    {
        var tenantId = context.Trigger.TenantId;
        await bus.SendReactionAsync(new AssignRolePermission(tenantId,
                BuiltInRbac.TenantAdministrationRoleId(tenantId),
                RbacPermissions.WorkforceManage), context, ct)
            .ConfigureAwait(false);
        await bus.SendReactionAsync(new AssignRolePermission(tenantId,
                BuiltInRbac.ComplianceManagementRoleId(tenantId),
                RbacPermissions.WorkforceManage), context, ct)
            .ConfigureAwait(false);
    }
}
