using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed class PermissionProjectionState
{
    public HashSet<Uuid> Members { get; init; } = [];
    public HashSet<Uuid> Roles { get; init; } = [];
    public Dictionary<Uuid, HashSet<string>> RolePermissions { get; init; } = [];
    public HashSet<Uuid> Teams { get; init; } = [];
    public HashSet<TeamMemberEdge> TeamMembers { get; init; } = [];
    public HashSet<TeamRoleEdge> TeamRoles { get; init; } = [];

    public void Apply(DomainEvent domainEvent)
    {
        switch (domainEvent)
        {
            case MemberRegistered member:
                Members.Add(member.MemberId);
                break;
            case TeamDefined team:
                Teams.Add(team.TeamId);
                break;
            case TeamDeleted team:
                Teams.Remove(team.TeamId);
                break;
            case TeamMemberAssigned teamMember:
                TeamMembers.Add(new TeamMemberEdge(teamMember.TeamId, teamMember.MemberId));
                break;
            case RoleDefined role:
                Roles.Add(role.RoleId);
                break;
            case RoleDeleted role:
                Roles.Remove(role.RoleId);
                break;
            case RolePermissionAssigned rolePermission:
                if (!RolePermissions.TryGetValue(rolePermission.RoleId, out var permissions))
                    RolePermissions[rolePermission.RoleId] = permissions = [];
                permissions.Add(rolePermission.Permission);
                break;
            case TeamRoleAssigned teamRole:
                TeamRoles.Add(new TeamRoleEdge(teamRole.TeamId, teamRole.RoleId));
                break;
        }
    }

    public IReadOnlySet<PermissionGrant> Materialize()
    {
        var activeTeamMembers = TeamMembers
            .Where(item => Members.Contains(item.MemberId) && Teams.Contains(item.TeamId));
        var activeTeamRoles = TeamRoles
            .Where(item => Teams.Contains(item.TeamId) && Roles.Contains(item.RoleId))
            .ToLookup(item => item.TeamId);
        var grants = new HashSet<PermissionGrant>();
        foreach (var teamMember in activeTeamMembers)
        {
            foreach (var teamRole in activeTeamRoles[teamMember.TeamId])
            {
                if (!RolePermissions.TryGetValue(teamRole.RoleId, out var permissions))
                    continue;
                foreach (var permission in permissions)
                    grants.Add(new PermissionGrant(teamMember.MemberId, permission));
            }
        }

        return grants;
    }
}
