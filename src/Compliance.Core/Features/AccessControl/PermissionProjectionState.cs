using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed class PermissionProjectionState
{
    public HashSet<Uuid> Members { get; init; } = [];
    public HashSet<Uuid> Roles { get; init; } = [];
    public HashSet<RolePermissionEdge> RolePermissions { get; init; } = [];
    public HashSet<Uuid> Teams { get; init; } = [];
    public HashSet<TeamMemberEdge> TeamMembers { get; init; } = [];
    public HashSet<MemberMembershipEpisode> MembershipEpisodes { get; init; } = [];
    public HashSet<TeamRoleEdge> TeamRoles { get; init; } = [];
    public Uuid TenantId { get; set; }
    // The tenant-local count of applied RBAC events, committed atomically with the projection checkpoint.
    public long Revision { get; set; }

    public void Apply(DomainEvent domainEvent)
    {
        TenantId = domainEvent switch
        {
            MemberRegistered ev => ev.TenantId,
            MemberSuspended ev => ev.TenantId,
            MemberReinstated ev => ev.TenantId,
            MemberDeprovisioned ev => ev.TenantId,
            TeamDefined ev => ev.TenantId,
            TeamDeleted ev => ev.TenantId,
            TeamMemberAssigned ev => ev.TenantId,
            TeamMemberRemoved ev => ev.TenantId,
            RoleDefined ev => ev.TenantId,
            RoleRenamed ev => ev.TenantId,
            RoleDeleted ev => ev.TenantId,
            RolePermissionAssigned ev => ev.TenantId,
            RolePermissionRemoved ev => ev.TenantId,
            TeamRoleAssigned ev => ev.TenantId,
            TeamRoleRemoved ev => ev.TenantId,
            _ => TenantId,
        };
        switch (domainEvent)
        {
            case MemberRegistered member:
                // Tenant team placement cannot grant standing permissions to firm staff.
                MembershipEpisodes.RemoveWhere(item => item.MemberId == member.MemberId);
                if (member.Affiliation == "firm_staff")
                    Members.Remove(member.MemberId);
                else
                {
                    Members.Add(member.MemberId);
                    MembershipEpisodes.Add(new MemberMembershipEpisode(member.MemberId,
                        member.MembershipEpisodeId));
                }
                break;
            case MemberSuspended member:
                Members.Remove(member.MemberId);
                break;
            case MemberDeprovisioned member:
                Members.Remove(member.MemberId);
                TeamMembers.RemoveWhere(item => item.MemberId == member.MemberId);
                MembershipEpisodes.RemoveWhere(item => item.MemberId == member.MemberId);
                break;
            case MemberReinstated member:
                if (member.Affiliation == "client_personnel")
                    Members.Add(member.MemberId);
                else
                    Members.Remove(member.MemberId);
                break;
            case TeamDefined team:
                Teams.Add(team.TeamId);
                break;
            case TeamDeleted team:
                Teams.Remove(team.TeamId);
                break;
            case TeamMemberAssigned teamMember:
                TeamMembers.RemoveWhere(item => item.TeamId == teamMember.TeamId &&
                    item.MemberId == teamMember.MemberId);
                TeamMembers.Add(new TeamMemberEdge(teamMember.TeamId, teamMember.MemberId,
                    teamMember.MembershipEpisodeId));
                break;
            case TeamMemberRemoved teamMember:
                TeamMembers.RemoveWhere(item => item.TeamId == teamMember.TeamId &&
                    item.MemberId == teamMember.MemberId);
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
        Revision = checked(Revision + 1);
    }

    public IReadOnlySet<PermissionGrant> Materialize()
    {
        var activeTeamMembers = TeamMembers
            .Where(item => Members.Contains(item.MemberId) && Teams.Contains(item.TeamId) &&
                GetMembershipEpisodeId(item.MemberId) == item.MembershipEpisodeId);
        var activeTeamRoles = TeamRoles
            .Where(item => Teams.Contains(item.TeamId) && Roles.Contains(item.RoleId) &&
                           BuiltInRbac.IsBuiltInRole(TenantId, item.RoleId))
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
        return TeamMembers.Where(item => item.MemberId == memberId && Teams.Contains(item.TeamId) &&
                GetMembershipEpisodeId(memberId) == item.MembershipEpisodeId)
            .SelectMany(item => rolesByTeam[item.TeamId].Select(role => new MemberAccessEdge(
                item.TeamId, role.RoleId, permissionsByRole[role.RoleId]
                    .Select(permission => permission.Permission)
                    .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray())))
            .OrderBy(item => item.TeamId.ToString(), StringComparer.Ordinal)
            .ThenBy(item => item.RoleId.ToString(), StringComparer.Ordinal)
            .ToArray();
    }

    Uuid GetMembershipEpisodeId(Uuid memberId) => MembershipEpisodes
        .Where(item => item.MemberId == memberId)
        .Select(item => item.MembershipEpisodeId)
        .FirstOrDefault();
}

readonly record struct MemberMembershipEpisode(Uuid MemberId, Uuid MembershipEpisodeId);
