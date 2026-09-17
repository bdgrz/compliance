using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>
///     Reads the tenant-membership directory directly from Fitz KV, independent of any projector's
///     workload scope — same direct-read pattern as
///     <see cref="Bdgrz.Compliance.Features.AccessControl.FitzTeamDirectoryReader" />.
/// </summary>
sealed class FitzTenantMembershipDirectoryReader(IKvClient client) : ITenantMembershipDirectoryReader
{
    public async ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
        await TenantMembershipDirectorySchema.Directory.GetAsync(
            client, TenantMembershipDirectoryKeys.Route(tenantId), userId, ct).ConfigureAwait(false) is not null;
}
