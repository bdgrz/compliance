using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Reads the team-role directory directly from Fitz KV, independent of any projector's workload
///     scope — same direct-read pattern as <see cref="FitzTeamMemberDirectoryReader" />.
/// </summary>
sealed class FitzTeamRoleDirectoryReader(IKvClient client)
    : FitzKvProjectionStore(client, TeamRoleDirectoryKeys.Route, "TeamRoleDirectory"),
      ITeamRoleDirectoryProjection,
      ITeamRoleDirectoryReader
{
    const int DefaultLimit = 50;
    const int MaxLimit = 200;

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case TeamRoleAssigned assigned:
                await TeamRoleDirectorySchema.Directory.InsertAsync(
                    Transaction, new TeamRoleView(assigned.TeamId, assigned.RoleId), ct).ConfigureAwait(false);
                break;
            case TeamRoleRemoved removed:
                await TeamRoleDirectorySchema.Directory.DeleteAsync(
                    Transaction, (removed.TeamId, removed.RoleId), ct).ConfigureAwait(false);
                break;
        }
    }

    public async ValueTask<Page<TeamRoleView>> ListAsync(
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
            var query = TeamRoleDirectorySchema.ByTeam.Query().WithPrefix(teamId.ToString()).After(cursor);
            if (limit is not null)
            {
                query = query.Take(limit.Value);
            }

            if (descending)
            {
                query = query.Descending();
            }

            return await TeamRoleDirectorySchema.Directory.QueryAsync(tx, query, ct).ConfigureAwait(false);
        }

        return await SearchAsync(tx, teamId, limit, cursor, search, descending, ct).ConfigureAwait(false);
    }

    // Same reasoning as FitzTeamMemberDirectoryReader.SearchAsync — see
    // design-api-contracts-hide-impl-strategy: Search means substring, not "whatever the index can
    // serve directly."
    static async ValueTask<Page<TeamRoleView>> SearchAsync(
        IKvTransaction tx, Uuid teamId, int? limit, string? cursor, string search, bool descending, CancellationToken ct)
    {
        var effectiveLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var matches = new List<TeamRoleView>();
        var scanCursor = cursor;
        while (matches.Count < effectiveLimit)
        {
            var step = TeamRoleDirectorySchema.ByTeam.Query().WithPrefix(teamId.ToString()).Take(1)
                .After(scanCursor);
            if (descending)
            {
                step = step.Descending();
            }

            var page = await TeamRoleDirectorySchema.Directory.QueryAsync(tx, step, ct)
                .ConfigureAwait(false);
            if (page.Items.Count == 0)
            {
                scanCursor = null;
                break;
            }

            if (page.Items[0].RoleId.ToString().Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(page.Items[0]);
            }

            scanCursor = page.NextCursor;
            if (scanCursor is null)
            {
                break;
            }
        }

        return new Page<TeamRoleView>(matches, matches.Count == effectiveLimit ? scanCursor : null);
    }
}
