using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Reads the team-member directory directly from Fitz KV, independent of any projector's
///     workload scope — same direct-read pattern as <see cref="FitzTeamDirectoryReader" />.
/// </summary>
sealed class FitzTeamMemberDirectoryReader(IKvClient client) : ITeamMemberDirectoryReader
{
    public ValueTask<Page<TeamMemberView>> ListAsync(
        Uuid tenantId,
        Uuid teamId,
        int? limit,
        string? cursor,
        string? search,
        bool descending,
        CancellationToken ct = default)
    {
        var query = TeamMemberDirectorySchema.ByTeam.Query().After(cursor);
        query = search is null
            ? query.WithPrefix(teamId.ToString())
            : query.WithPrefix(teamId.ToString(), search.ToLowerInvariant());
        if (limit is not null)
        {
            query = query.Take(limit.Value);
        }

        if (descending)
        {
            query = query.Descending();
        }

        return TeamMemberDirectorySchema.Directory.QueryAsync(
            client, TeamMemberDirectoryKeys.Route(tenantId.ToString()), query, ct);
    }
}
