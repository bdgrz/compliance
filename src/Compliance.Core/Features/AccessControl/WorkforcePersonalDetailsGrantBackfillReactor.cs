using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Replays historical tenant registrations to grant personal-details read to Tenant
///     Administration and Compliance Management. Its own checkpoint preserves the manager-chain
///     backfill's progress and any later manager-chain grant removals.
/// </summary>
public sealed partial class WorkforcePersonalDetailsGrantBackfillReactor(
    IProjectionCheckpointStore checkpoints, IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForPattern("bdgrz", "tenants"), WorkloadName),
      IReactorHandler<TenantRegistered>
{
    public const string WorkloadName = "WorkforcePersonalDetailsGrantBackfillV1";

    public async ValueTask HandleAsync(IReactorContext<TenantRegistered> context,
        CancellationToken ct)
    {
        var tenantId = context.Trigger.TenantId;
        await bus.SendReactionAsync(new AssignRolePermission(tenantId,
                BuiltInRbac.TenantAdministrationRoleId(tenantId),
                FieldClasses.WorkforcePersonalDetails.ReadPermission), context, ct)
            .ConfigureAwait(false);
        await bus.SendReactionAsync(new AssignRolePermission(tenantId,
                BuiltInRbac.ComplianceManagementRoleId(tenantId),
                FieldClasses.WorkforcePersonalDetails.ReadPermission), context, ct)
            .ConfigureAwait(false);
    }
}
