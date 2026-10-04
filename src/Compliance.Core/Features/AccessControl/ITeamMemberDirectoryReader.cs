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

    /// <summary>Checks event history after this directory's checkpoint for a removal not yet projected.</summary>
    ValueTask<bool> HasPendingRemovalAsync(Uuid tenantId, Uuid teamId, Uuid memberId,
        CancellationToken ct = default) => ValueTask.FromResult(false);

    /// <summary>Returns every current team assignment for one member, including unprojected events.</summary>
    ValueTask<IReadOnlyList<TeamMemberView>> ListMemberAssignmentsAsync(Uuid tenantId,
        Uuid memberId, CancellationToken ct = default) =>
        ValueTask.FromResult<IReadOnlyList<TeamMemberView>>([]);
}
