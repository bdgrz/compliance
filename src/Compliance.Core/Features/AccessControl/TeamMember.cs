using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class TeamMember : Aggregate
{
    readonly Uuid _tenantId;
    readonly Uuid _teamId;
    readonly Uuid _memberId;
    bool _isAssigned;

    public TeamMember(Uuid tenantId, Uuid teamId, Uuid memberId)
        : base(
            RbacIds.TeamMember(tenantId, teamId, memberId),
            new EventStreamAddress(
                tenantId.ToString(),
                "rbac-team-members",
                RbacIds.TeamMember(tenantId, teamId, memberId).ToString()))
    {
        _tenantId = tenantId;
        _teamId = teamId;
        _memberId = memberId;
        On<TeamMemberAssigned>(_ => _isAssigned = true);
    }

    public Result Assign()
    {
        if (!_isAssigned)
            RaiseEvent(new TeamMemberAssigned(_tenantId, _teamId, _memberId));
        return Result.Success;
    }
}
