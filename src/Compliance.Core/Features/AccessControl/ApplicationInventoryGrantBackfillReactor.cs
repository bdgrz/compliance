using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
/// Replays historical tenant registrations to add only the inventory permission.
/// </summary>
public sealed partial class ApplicationInventoryGrantBackfillReactor(
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
                RbacPermissions.ApplicationInventoryManage), context, ct)
            .ConfigureAwait(false);
        await bus.SendReactionAsync(new AssignRolePermission(tenantId,
                BuiltInRbac.ComplianceManagementRoleId(tenantId),
                RbacPermissions.ApplicationInventoryManage), context, ct)
            .ConfigureAwait(false);
    }
}
