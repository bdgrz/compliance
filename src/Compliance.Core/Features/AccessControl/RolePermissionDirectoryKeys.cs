namespace Bdgrz.Compliance.Features.AccessControl;

static class RolePermissionDirectoryKeys
{
    public static string Route(string tenantId) => $"kv://bdgrz/role-permission-directory/{tenantId}";
}
