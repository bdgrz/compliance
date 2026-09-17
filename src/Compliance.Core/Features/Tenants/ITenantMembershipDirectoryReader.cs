using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>Reads one tenant's materialized membership directory.</summary>
public interface ITenantMembershipDirectoryReader
{
    ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default);
}
