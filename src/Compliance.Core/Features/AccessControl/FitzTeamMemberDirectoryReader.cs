using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Serves the "TeamMemberDirectory" projector: writes join its batch transaction, and
///     query-side reads open their own read-only transaction on the resource it writes for the
///     given tenant.
/// </summary>
sealed class FitzTeamMemberDirectoryReader(IKvClient client)
    : FitzKvProjectionStore(client, TeamMemberDirectoryKeys.Route, "TeamMemberDirectory"),
      ITeamMemberDirectoryProjection,
      ITeamMemberDirectoryReader
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case TeamMemberAssigned assigned:
                await TeamMemberDirectorySchema.Directory.InsertAsync(
                    Transaction,
                    new TeamMemberView(assigned.TeamId, assigned.MemberId),
                    ct).ConfigureAwait(false);
                break;
            case TeamMemberRemoved removed:
                await TeamMemberDirectorySchema.Directory.DeleteAsync(
                    Transaction,
                    (removed.TeamId, removed.MemberId),
                    ct).ConfigureAwait(false);
                break;
        }
    }

    public async ValueTask<Page<TeamMemberView>> ListAsync(
        Uuid tenantId,
        Uuid teamId,
        int? limit,
        string? cursor,
        string? search,
        bool descending,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
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

            return await TeamMemberDirectorySchema.Directory.QueryAsync(tx, query, ct).ConfigureAwait(false);
        }

        return await SearchAsync(tx, teamId, limit, cursor, search, descending, ct).ConfigureAwait(false);
    }

    // Same reasoning as FitzTeamDirectoryReader.SearchAsync: the by_team index prefix only bounds
    // the scan to one team, it cannot substring-match member IDs, so a real search walks that team's
    // member-id-ordered range one entry at a time and filters here. See
    // design-api-contracts-hide-impl-strategy — Search means substring, not "whatever the index can
    // serve directly."
    static async ValueTask<Page<TeamMemberView>> SearchAsync(
        IKvTransaction tx, Uuid teamId, int? limit, string? cursor, string search, bool descending, CancellationToken ct)
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

            var page = await TeamMemberDirectorySchema.Directory.QueryAsync(tx, step, ct).ConfigureAwait(false);
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
