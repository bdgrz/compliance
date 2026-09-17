using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Reads the materialized team directory for a tenant.</summary>
public interface ITeamDirectoryReader
{
    ValueTask<TeamView?> GetAsync(Uuid tenantId, Uuid teamId, CancellationToken ct = default);

    /// <summary>Lists a tenant's teams by name.</summary>
    /// <param name="search">A case-insensitive name prefix filter, or <see langword="null"/> for none.</param>
    ValueTask<Page<TeamView>> ListAsync(
        Uuid tenantId,
        int? limit,
        string? cursor,
        string? search,
        bool descending,
        CancellationToken ct = default);
}
