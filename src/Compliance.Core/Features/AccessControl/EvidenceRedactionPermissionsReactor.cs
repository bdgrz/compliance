using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Backfills fixed-role redaction permissions without granting artifact access or changing membership.</summary>
public sealed partial class EvidenceRedactionPermissionsReactor(IProjectionCheckpointStore checkpoints, IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForPattern("bdgrz", "tenants"), "EvidenceRedactionPermissionsV1"),
      IReactorHandler<TenantRegistered>
{
    public async ValueTask HandleAsync(IReactorContext<TenantRegistered> context, CancellationToken ct)
    {
        var tenant = context.Trigger.TenantId;
        AssignRolePermission[] commands =
        [
            new(tenant, BuiltInRbac.TenantAdministrationRoleId(tenant), RbacPermissions.EvidenceRedactionPrepare),
            new(tenant, BuiltInRbac.ComplianceManagementRoleId(tenant), RbacPermissions.EvidenceRedactionPrepare),
            new(tenant, BuiltInRbac.ComplianceParticipationRoleId(tenant), RbacPermissions.EvidenceRedactionPrepare),
            new(tenant, BuiltInRbac.ComplianceManagementRoleId(tenant), RbacPermissions.EvidenceRedactionApprove),
        ];
        foreach (var command in commands)
            await bus.SendReactionAsync(command, context, ct).ConfigureAwait(false);
    }
}
