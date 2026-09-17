namespace Bdgrz.Compliance.Features.AccessControl;

static class TeamDirectoryKeys
{
    public static string Route(string tenantId) => $"kv://bdgrz/team-directory/{tenantId}";
}
