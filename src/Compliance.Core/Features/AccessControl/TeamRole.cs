using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class TeamRole : Aggregate
{
    readonly Uuid _tenantId;
    readonly Uuid _teamId;
    readonly Uuid _roleId;
    bool _isAssigned;

    public TeamRole(Uuid tenantId, Uuid teamId, Uuid roleId)
        : base(
            RbacIds.TeamRole(tenantId, teamId, roleId),
            new EventStreamAddress(
                tenantId.ToString(),
                "rbac-team-roles",
                RbacIds.TeamRole(tenantId, teamId, roleId).ToString()))
    {
        _tenantId = tenantId;
        _teamId = teamId;
        _roleId = roleId;
        On<TeamRoleAssigned>(_ => _isAssigned = true);
    }

    public Result Assign()
    {
        if (!_isAssigned)
            RaiseEvent(new TeamRoleAssigned(_tenantId, _teamId, _roleId));
        return Result.Success;
    }
}
