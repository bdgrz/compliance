using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Reads the materialized team-member directory for one team.</summary>
public interface ITeamMemberDirectoryReader
{
    /// <summary>Lists one team's members by member ID.</summary>
    /// <param name="search">A member-ID prefix filter, or <see langword="null"/> for none.</param>
    ValueTask<Page<TeamMemberView>> ListAsync(
        Uuid tenantId,
        Uuid teamId,
        int? limit,
        string? cursor,
        string? search,
        bool descending,
        CancellationToken ct = default);
}
