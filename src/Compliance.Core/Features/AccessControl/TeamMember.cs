using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class TeamMember : Aggregate
{
    readonly Uuid _tenantId;
    readonly Uuid _teamId;
    readonly Uuid _memberId;
    bool _isAssigned;
    Uuid _membershipEpisodeId;

    public bool IsAssigned => _isAssigned;
    public Uuid MembershipEpisodeId => _membershipEpisodeId;

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
        On<TeamMemberAssigned>(assigned =>
        {
            _isAssigned = true;
            _membershipEpisodeId = assigned.MembershipEpisodeId;
        });
        On<TeamMemberRemoved>(_ =>
        {
            _isAssigned = false;
            _membershipEpisodeId = Uuid.Empty;
        });
    }

    public Result Assign(Uuid membershipEpisodeId = default)
    {
        if (!_isAssigned || _membershipEpisodeId != membershipEpisodeId)
            RaiseEvent(new TeamMemberAssigned(_tenantId, _teamId, _memberId, membershipEpisodeId));
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
