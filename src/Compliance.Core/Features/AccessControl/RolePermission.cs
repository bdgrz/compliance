using System.Diagnostics.CodeAnalysis;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "RolePermission is the canonical RBAC relationship term.")]
public sealed class RolePermission : Aggregate
{
    readonly Uuid _tenantId;
    readonly Uuid _roleId;
    readonly string _permission;
    bool _isAssigned;

    public RolePermission(Uuid tenantId, Uuid roleId, string permission)
        : base(
            RbacIds.RolePermission(tenantId, roleId, Permissions.Normalize(permission)),
            new EventStreamAddress(
                tenantId.ToString(),
                "rbac-role-permissions",
                RbacIds.RolePermission(tenantId, roleId, Permissions.Normalize(permission)).ToString()))
    {
        _tenantId = tenantId;
        _roleId = roleId;
        _permission = Permissions.Normalize(permission);
        On<RolePermissionAssigned>(_ => _isAssigned = true);
    }

    public Result Assign()
    {
        if (!_isAssigned)
            RaiseEvent(new RolePermissionAssigned(_tenantId, _roleId, _permission));
        return Result.Success;
    }
}
