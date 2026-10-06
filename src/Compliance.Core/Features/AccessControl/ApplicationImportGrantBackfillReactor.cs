using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Grants import staging on new and historical tenant registrations using an independent checkpoint.</summary>
public sealed partial class ApplicationImportGrantBackfillReactor(
    IProjectionCheckpointStore checkpoints, IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForPattern("bdgrz", "tenants"), WorkloadName),
      IReactorHandler<TenantRegistered>
{
    public const string WorkloadName = "ApplicationImportGrantBackfillV1";

    public async ValueTask HandleAsync(IReactorContext<TenantRegistered> context,
        CancellationToken ct)
    {
        var tenantId = context.Trigger.TenantId;
        var roles = new[]
        {
            BuiltInRbac.TenantAdministrationRoleId(tenantId),
            BuiltInRbac.ComplianceManagementRoleId(tenantId),
            BuiltInRbac.ComplianceParticipationRoleId(tenantId),
        };
        foreach (var roleId in roles)
            await bus.SendReactionAsync(new AssignRolePermission(tenantId, roleId,
                    RbacPermissions.ApplicationImportStage), context, ct)
                .ConfigureAwait(false);
    }
}
