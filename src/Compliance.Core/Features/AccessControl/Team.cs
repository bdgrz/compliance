using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class Team : Aggregate
{
    readonly Uuid _tenantId;
    bool _isDefined;
    bool _isDeleted;

    public Team(Uuid tenantId, Uuid teamId)
        : base(teamId, new EventStreamAddress(tenantId.ToString(), "rbac-teams", teamId.ToString()))
    {
        _tenantId = tenantId;
        On<TeamDefined>(_ => _isDefined = true);
        On<TeamDeleted>(_ => _isDeleted = true);
    }

    public Result Define(string name)
    {
        if (_isDefined)
            return Result.Success;
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "A team requires a name."));
        RaiseEvent(new TeamDefined(_tenantId, Id, name.Trim()));
        return Result.Success;
    }

    public Result Delete()
    {
        if (BuiltInRbac.IsBuiltInTeam(_tenantId, Id))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "Built-in teams cannot be deleted."));
        if (!_isDefined)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The team is not defined."));
        if (!_isDeleted)
            RaiseEvent(new TeamDeleted(_tenantId, Id));
        return Result.Success;
    }
}
