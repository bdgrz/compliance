using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>Reads the materialized tenant-membership directory for one user.</summary>
public interface ITenantMembershipDirectoryReader
{
    ValueTask<Page<TenantMembershipView>> ListByUserAsync(
        Uuid userId,
        int? limit,
        string? cursor,
        bool descending,
        CancellationToken ct = default);
}
