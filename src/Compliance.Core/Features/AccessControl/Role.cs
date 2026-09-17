using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class Role : Aggregate
{
    readonly Uuid _tenantId;
    bool _isDefined;
    bool _isDeleted;

    public Role(Uuid tenantId, Uuid roleId)
        : base(roleId, new EventStreamAddress(tenantId.ToString(), "rbac-roles", roleId.ToString()))
    {
        _tenantId = tenantId;
        On<RoleDefined>(_ => _isDefined = true);
        On<RoleDeleted>(_ => _isDeleted = true);
    }

    public Result Define(string name)
    {
        if (_isDefined)
            return Result.Success;
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "A role requires a name."));
        RaiseEvent(new RoleDefined(_tenantId, Id, name.Trim()));
        return Result.Success;
    }

    public Result Delete()
    {
        if (BuiltInRbac.IsBuiltInRole(_tenantId, Id))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "Built-in roles cannot be deleted."));
        if (!_isDefined)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The role is not defined."));
        if (!_isDeleted)
            RaiseEvent(new RoleDeleted(_tenantId, Id));
        return Result.Success;
    }
}
