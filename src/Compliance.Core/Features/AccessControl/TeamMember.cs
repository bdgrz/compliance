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
        On<TeamMemberRemoved>(_ => _isAssigned = false);
    }

    public Result Assign()
    {
        if (!_isAssigned)
            RaiseEvent(new TeamMemberAssigned(_tenantId, _teamId, _memberId));
        return Result.Success;
    }

    public Result Remove()
    {
        if (!_isAssigned)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The member is not on this team."));
        RaiseEvent(new TeamMemberRemoved(_tenantId, _teamId, _memberId));
        return Result.Success;
    }
}
