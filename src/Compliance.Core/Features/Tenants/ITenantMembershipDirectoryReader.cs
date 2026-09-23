using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>Reads one tenant's materialized membership directory.</summary>
public interface ITenantMembershipDirectoryReader
{
    ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId, CancellationToken ct = default);
    ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default);
    ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit, string? cursor,
        CancellationToken ct = default);
}
