using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Reads the materialized team-role directory for one team.</summary>
public interface ITeamRoleDirectoryReader
{
    /// <summary>Lists the roles granted to one team.</summary>
    /// <param name="search">A role-ID substring filter, or <see langword="null"/> for none.</param>
    ValueTask<Page<TeamRoleView>> ListAsync(
        Uuid tenantId,
        Uuid teamId,
        int? limit,
        string? cursor,
        string? search,
        bool descending,
        CancellationToken ct = default);
}
