using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class Role : Aggregate
{
    readonly Uuid _tenantId;
    bool _isDefined;
    bool _isDeleted;
    string? _name;

    public bool IsActive => _isDefined && !_isDeleted;

    public Role(Uuid tenantId, Uuid roleId)
        : base(roleId, new EventStreamAddress(tenantId.ToString(), "rbac-roles", roleId.ToString()))
    {
        _tenantId = tenantId;
        On<RoleDefined>(ev => { _isDefined = true; _name = ev.Name; });
        On<RoleRenamed>(ev => _name = ev.Name);
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

    public Result Rename(string name)
    {
        if (_isDeleted)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The role is not defined."));
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "A role requires a name."));
        var normalized = name.Trim();
        if (!_isDefined)
        {
            RaiseEvent(new RoleDefined(_tenantId, Id, normalized));
            return Result.Success;
        }
        if (!string.Equals(_name, normalized, StringComparison.Ordinal))
            RaiseEvent(new RoleRenamed(_tenantId, Id, normalized));
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
