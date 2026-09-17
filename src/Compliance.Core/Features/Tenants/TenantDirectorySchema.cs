using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>The shared tenant directory schema, used by both the read and write sides.</summary>
static class TenantDirectorySchema
{
    public static readonly KvDirectory<TenantView, Uuid> Directory = new(
        "tenants-by-id",
        ComplianceCoreJsonContext.Default.TenantView,
        static tenant => tenant.TenantId,
        static tenantId => [tenantId.ToString()],
        []);
}
