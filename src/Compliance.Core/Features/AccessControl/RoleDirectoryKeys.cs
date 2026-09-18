namespace Bdgrz.Compliance.Features.AccessControl;

static class RoleDirectoryKeys
{
    public static string Route(string tenantId) => $"kv://bdgrz/role-directory/{tenantId}";
}
