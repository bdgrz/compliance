using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Reads the materialized role-permission directory for one role.</summary>
public interface IRolePermissionDirectoryReader
{
    /// <summary>Lists one role's permissions.</summary>
    /// <param name="search">A permission-substring filter, or <see langword="null"/> for none.</param>
    ValueTask<Page<RolePermissionView>> ListAsync(
        Uuid tenantId,
        Uuid roleId,
        int? limit,
        string? cursor,
        string? search,
        bool descending,
        CancellationToken ct = default);
}
