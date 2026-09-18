namespace Bdgrz.Compliance.Features.AccessControl;

static class RoleTeamDirectoryKeys
{
    public static string Route(string tenantId) => $"kv://bdgrz/role-team-directory/{tenantId}";
}
