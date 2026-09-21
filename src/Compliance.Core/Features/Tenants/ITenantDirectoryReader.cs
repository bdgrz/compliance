using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>Reads the materialized tenant directory.</summary>
public interface ITenantDirectoryReader
{
    ValueTask<TenantView?> GetAsync(Uuid tenantId, CancellationToken ct = default);
    ValueTask<Page<TenantView>> ListAsync(int limit, string? cursor, CancellationToken ct = default);
}
