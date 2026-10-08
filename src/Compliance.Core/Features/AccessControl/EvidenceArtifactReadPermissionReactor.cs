using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Installs one fixed Viewer permission for existing and new tenants; never issues artifact access grants.</summary>
public sealed partial class EvidenceArtifactReadPermissionReactor(IProjectionCheckpointStore checkpoints, IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForPattern("bdgrz", "tenants"), "EvidenceArtifactReadPermissionV1"),
      IReactorHandler<TenantRegistered>
{
    public ValueTask HandleAsync(IReactorContext<TenantRegistered> context, CancellationToken ct) =>
        bus.SendReactionAsync(new AssignRolePermission(context.Trigger.TenantId,
            BuiltInRbac.ViewerRoleId(context.Trigger.TenantId), RbacPermissions.EvidenceArtifactRead), context, ct);
}
