namespace Bdgrz.Compliance.Features.AccessControl;

static class TeamMemberDirectoryKeys
{
    public static string Route(string tenantId) => $"kv://bdgrz/team-member-directory/{tenantId}";
}
