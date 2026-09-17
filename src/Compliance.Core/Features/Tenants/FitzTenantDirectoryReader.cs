using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>
///     Reads the tenant directory directly from Fitz KV, independent of any projector's workload
///     scope — same direct-read pattern as <see cref="Bdgrz.Compliance.Features.AccessControl.FitzTeamDirectoryReader" />.
/// </summary>
sealed class FitzTenantDirectoryReader(IKvClient client) : ITenantDirectoryReader
{
    public ValueTask<TenantView?> GetAsync(Uuid tenantId, CancellationToken ct = default) =>
        TenantDirectorySchema.Directory.GetAsync(client, TenantDirectoryKeys.Route(), tenantId, ct);
}
