using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Reads the materialized role directory for a tenant.</summary>
public interface IRoleDirectoryReader
{
    ValueTask<RoleView?> GetAsync(Uuid tenantId, Uuid roleId, CancellationToken ct = default);

    /// <summary>Lists a tenant's roles by name.</summary>
    /// <param name="search">A case-insensitive name substring filter, or <see langword="null"/> for none.</param>
    ValueTask<Page<RoleView>> ListAsync(
        Uuid tenantId,
        int? limit,
        string? cursor,
        string? search,
        bool descending,
        CancellationToken ct = default);
}
