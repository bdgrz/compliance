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
    const int DefaultLimit = 50;
    const int MaxLimit = 200;

    public ValueTask<Page<TeamMemberView>> ListAsync(
        Uuid tenantId,
        Uuid teamId,
        int? limit,
        string? cursor,
        string? search,
        bool descending,
        CancellationToken ct = default)
    {
        var route = TeamMemberDirectoryKeys.Route(tenantId.ToString());
        if (search is null)
        {
            var query = TeamMemberDirectorySchema.ByTeam.Query().WithPrefix(teamId.ToString()).After(cursor);
            if (limit is not null)
            {
                query = query.Take(limit.Value);
            }

            if (descending)
            {
                query = query.Descending();
            }

            return TeamMemberDirectorySchema.Directory.QueryAsync(client, route, query, ct);
        }

        return SearchAsync(route, teamId, limit, cursor, search, descending, ct);
    }

    // Same reasoning as FitzTeamDirectoryReader.SearchAsync: the by_team index prefix only bounds
    // the scan to one team, it cannot substring-match member IDs, so a real search walks that team's
    // member-id-ordered range one entry at a time and filters here. See
    // design-api-contracts-hide-impl-strategy — Search means substring, not "whatever the index can
    // serve directly."
    async ValueTask<Page<TeamMemberView>> SearchAsync(
        string route, Uuid teamId, int? limit, string? cursor, string search, bool descending, CancellationToken ct)
    {
        var effectiveLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var matches = new List<TeamMemberView>();
        var scanCursor = cursor;
        while (matches.Count < effectiveLimit)
        {
            var step = TeamMemberDirectorySchema.ByTeam.Query().WithPrefix(teamId.ToString()).Take(1).After(scanCursor);
            if (descending)
            {
                step = step.Descending();
            }

            var page = await TeamMemberDirectorySchema.Directory.QueryAsync(client, route, step, ct)
                .ConfigureAwait(false);
            if (page.Items.Count == 0)
            {
                scanCursor = null;
                break;
            }

            if (page.Items[0].MemberId.ToString().Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(page.Items[0]);
            }

            scanCursor = page.NextCursor;
            if (scanCursor is null)
            {
                break;
            }
        }

        return new Page<TeamMemberView>(matches, matches.Count == effectiveLimit ? scanCursor : null);
    }
}
