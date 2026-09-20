using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed class PermissionProjectionState
{
    public HashSet<Uuid> Members { get; init; } = [];
    public HashSet<Uuid> Roles { get; init; } = [];
    public HashSet<RolePermissionEdge> RolePermissions { get; init; } = [];
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
            case TeamMemberRemoved teamMember:
                TeamMembers.Remove(new TeamMemberEdge(teamMember.TeamId, teamMember.MemberId));
                break;
            case RoleDefined role:
                Roles.Add(role.RoleId);
                break;
            case RoleDeleted role:
                Roles.Remove(role.RoleId);
                break;
            case RolePermissionAssigned rolePermission:
                RolePermissions.Add(new RolePermissionEdge(rolePermission.RoleId, rolePermission.Permission));
                break;
            case RolePermissionRemoved rolePermission:
                RolePermissions.Remove(new RolePermissionEdge(rolePermission.RoleId, rolePermission.Permission));
                break;
            case TeamRoleAssigned teamRole:
                TeamRoles.Add(new TeamRoleEdge(teamRole.TeamId, teamRole.RoleId));
                break;
            case TeamRoleRemoved teamRole:
                TeamRoles.Remove(new TeamRoleEdge(teamRole.TeamId, teamRole.RoleId));
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
        var rolePermissions = RolePermissions.ToLookup(item => item.RoleId);
        var grants = new HashSet<PermissionGrant>();
        foreach (var teamMember in activeTeamMembers)
        {
            foreach (var teamRole in activeTeamRoles[teamMember.TeamId])
            {
                foreach (var rolePermission in rolePermissions[teamRole.RoleId])
                    grants.Add(new PermissionGrant(teamMember.MemberId, rolePermission.Permission));
            }
        }

        return grants;
    }

    public IReadOnlyList<MemberAccessEdge> Explain(Uuid memberId)
    {
        if (!Members.Contains(memberId))
            return [];
        var rolesByTeam = TeamRoles.Where(item => Teams.Contains(item.TeamId) &&
                Roles.Contains(item.RoleId))
            .ToLookup(item => item.TeamId);
        var permissionsByRole = RolePermissions.ToLookup(item => item.RoleId);
        return TeamMembers.Where(item => item.MemberId == memberId && Teams.Contains(item.TeamId))
            .SelectMany(item => rolesByTeam[item.TeamId].Select(role => new MemberAccessEdge(
                item.TeamId, role.RoleId, permissionsByRole[role.RoleId]
                    .Select(permission => permission.Permission)
                    .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray())))
            .OrderBy(item => item.TeamId.ToString(), StringComparer.Ordinal)
            .ThenBy(item => item.RoleId.ToString(), StringComparer.Ordinal)
            .ToArray();
    }
}
