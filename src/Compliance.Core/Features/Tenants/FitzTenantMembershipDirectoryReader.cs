using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>
///     Reads the tenant-membership directory directly from Fitz KV, independent of any projector's
///     workload scope — same direct-read pattern as
///     <see cref="Bdgrz.Compliance.Features.AccessControl.FitzTeamMemberDirectoryReader" />.
/// </summary>
sealed class FitzTenantMembershipDirectoryReader(IKvClient client) : ITenantMembershipDirectoryReader
{
    public ValueTask<Page<TenantMembershipView>> ListByUserAsync(
        Uuid userId,
        int? limit,
        string? cursor,
        bool descending,
        CancellationToken ct = default)
    {
        var query = TenantMembershipDirectorySchema.ByUser.Query().WithPrefix(userId.ToString()).After(cursor);
        if (limit is not null)
        {
            query = query.Take(limit.Value);
        }

        if (descending)
        {
            query = query.Descending();
        }

        return TenantMembershipDirectorySchema.Directory.QueryAsync(
            client, TenantMembershipDirectoryKeys.Route(), query, ct);
    }
}
