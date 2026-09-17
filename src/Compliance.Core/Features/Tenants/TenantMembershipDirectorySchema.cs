using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>
///     The shared tenant-membership directory schema, used by both the read and write sides. Routed
///     per tenant (see <see cref="TenantMembershipDirectoryKeys" />), so identity is just the member's
///     UserId — TenantId is already implied by which tenant's route a row lives in.
/// </summary>
static class TenantMembershipDirectorySchema
{
    public static readonly KvDirectory<TenantMembershipView, Uuid> Directory = new(
        "tenant-memberships",
        ComplianceCoreJsonContext.Default.TenantMembershipView,
        static membership => membership.UserId,
        static userId => [userId.ToString()],
        []);
}
