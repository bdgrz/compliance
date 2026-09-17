using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>The shared tenant-membership directory schema, used by both the read and write sides.</summary>
static class TenantMembershipDirectorySchema
{
    public static readonly KvDirectoryIndex<TenantMembershipView> ByUser = new(
        "by_user", 1, static membership => [membership.UserId.ToString(), membership.TenantId.ToString()]);

    public static readonly KvDirectory<TenantMembershipView, (Uuid UserId, Uuid TenantId)> Directory = new(
        "tenant-memberships",
        ComplianceCoreJsonContext.Default.TenantMembershipView,
        static membership => (membership.UserId, membership.TenantId),
        static identity => [identity.UserId.ToString(), identity.TenantId.ToString()],
        [ByUser]);
}
