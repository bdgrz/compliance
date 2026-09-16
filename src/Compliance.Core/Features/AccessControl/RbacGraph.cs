using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class RbacGraph(Uuid tenantId)
{
    readonly HashSet<Uuid> _members = [];
    readonly HashSet<Uuid> _roles = [];
    readonly Dictionary<Uuid, HashSet<string>> _rolePermissions = [];
    readonly HashSet<Uuid> _teams = [];
    readonly List<TeamMemberAssigned> _teamMembers = [];
    readonly List<TeamRoleAssigned> _teamRoles = [];

    public void Apply(DomainEvent domainEvent)
    {
        if (TenantId(domainEvent) != tenantId)
            throw new InvalidOperationException("An RBAC graph cannot contain another tenant's events.");

        switch (domainEvent)
        {
            case MemberRegistered member:
                _members.Add(member.MemberId);
                break;
            case TeamDefined team:
                _teams.Add(team.TeamId);
                break;
            case TeamDeleted team:
                _teams.Remove(team.TeamId);
                break;
            case TeamMemberAssigned teamMember:
                _teamMembers.Add(teamMember);
                break;
            case RoleDefined role:
                _roles.Add(role.RoleId);
                break;
            case RoleDeleted role:
                _roles.Remove(role.RoleId);
                break;
            case RolePermissionAssigned rolePermission:
                if (!_rolePermissions.TryGetValue(rolePermission.RoleId, out var permissions))
                    _rolePermissions[rolePermission.RoleId] = permissions = [];
                permissions.Add(rolePermission.Permission);
                break;
            case TeamRoleAssigned teamRole:
                _teamRoles.Add(teamRole);
                break;
        }
    }

    public IReadOnlyList<string> GetPermissions(Uuid memberId)
    {
        if (!_members.Contains(memberId))
            return [];
        var teams = _teamMembers
            .Where(item => item.MemberId == memberId && _teams.Contains(item.TeamId))
            .Select(item => item.TeamId)
            .ToHashSet();
        var roles = _teamRoles
            .Where(item => teams.Contains(item.TeamId) && _roles.Contains(item.RoleId))
            .Select(item => item.RoleId)
            .ToHashSet();
        return _rolePermissions
            .Where(item => roles.Contains(item.Key))
            .SelectMany(item => item.Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    static Uuid TenantId(DomainEvent domainEvent) => domainEvent switch
    {
        MemberRegistered item => item.TenantId,
        TeamDefined item => item.TenantId,
        TeamDeleted item => item.TenantId,
        TeamMemberAssigned item => item.TenantId,
        RoleDefined item => item.TenantId,
        RoleDeleted item => item.TenantId,
        RolePermissionAssigned item => item.TenantId,
        TeamRoleAssigned item => item.TenantId,
        _ => throw new ArgumentException("The event is not an RBAC event.", nameof(domainEvent)),
    };
}
