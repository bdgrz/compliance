using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Reads the materialized role-team directory for one role.</summary>
public interface IRoleTeamDirectoryReader
{
    /// <summary>Lists the teams holding one role.</summary>
    /// <param name="search">A team-ID substring filter, or <see langword="null"/> for none.</param>
    ValueTask<Page<RoleTeamView>> ListAsync(
        Uuid tenantId,
        Uuid roleId,
        int? limit,
        string? cursor,
        string? search,
        bool descending,
        CancellationToken ct = default);
}
