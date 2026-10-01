using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Replays tenant registrations to grant the restricted workforce manager-chain field to Tenant
///     Administration only (M0-D06 defaults). Other roles receive it only by explicit assignment.
/// </summary>
public sealed partial class WorkforceRestrictedFieldGrantBackfillReactor(
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
                FieldClasses.WorkforceManagerChain.ReadPermission), context, ct)
            .ConfigureAwait(false);
    }
}
