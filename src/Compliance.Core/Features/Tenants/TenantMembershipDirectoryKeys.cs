namespace Bdgrz.Compliance.Features.Tenants;

static class TenantMembershipDirectoryKeys
{
    public static string Route(string tenantId) => $"kv://{tenantId}/tenant-membership-directory/by-user";
}
