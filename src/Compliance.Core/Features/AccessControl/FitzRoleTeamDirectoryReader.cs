using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Reads the role-team directory directly from Fitz KV, independent of any projector's workload
///     scope — same direct-read pattern as <see cref="FitzTeamMemberDirectoryReader" />.
/// </summary>
sealed class FitzRoleTeamDirectoryReader(IKvClient client)
    : FitzKvProjectionStore(client, RoleTeamDirectoryKeys.Route, "RoleTeamDirectory"),
      IRoleTeamDirectoryProjection,
      IRoleTeamDirectoryReader
{
    const int DefaultLimit = 50;
    const int MaxLimit = 200;

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case TeamRoleAssigned assigned:
                await RoleTeamDirectorySchema.Directory.InsertAsync(
                    Transaction, new RoleTeamView(assigned.RoleId, assigned.TeamId), ct).ConfigureAwait(false);
                break;
            case TeamRoleRemoved removed:
                await RoleTeamDirectorySchema.Directory.DeleteAsync(
                    Transaction, (removed.RoleId, removed.TeamId), ct).ConfigureAwait(false);
                break;
        }
    }

    public async ValueTask<Page<RoleTeamView>> ListAsync(
        Uuid tenantId,
        Uuid roleId,
        int? limit,
        string? cursor,
        string? search,
        bool descending,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        if (search is null)
        {
            var query = RoleTeamDirectorySchema.ByRole.Query().WithPrefix(roleId.ToString()).After(cursor);
            if (limit is not null)
            {
                query = query.Take(limit.Value);
            }

            if (descending)
            {
                query = query.Descending();
            }

            return await RoleTeamDirectorySchema.Directory.QueryAsync(tx, query, ct).ConfigureAwait(false);
        }

        return await SearchAsync(tx, roleId, limit, cursor, search, descending, ct).ConfigureAwait(false);
    }

    // Same reasoning as FitzTeamMemberDirectoryReader.SearchAsync — see
    // design-api-contracts-hide-impl-strategy: Search means substring, not "whatever the index can
    // serve directly."
    static async ValueTask<Page<RoleTeamView>> SearchAsync(
        IKvTransaction tx, Uuid roleId, int? limit, string? cursor, string search, bool descending, CancellationToken ct)
    {
        var effectiveLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var matches = new List<RoleTeamView>();
        var scanCursor = cursor;
        while (matches.Count < effectiveLimit)
        {
            var step = RoleTeamDirectorySchema.ByRole.Query().WithPrefix(roleId.ToString()).Take(1)
                .After(scanCursor);
            if (descending)
            {
                step = step.Descending();
            }

            var page = await RoleTeamDirectorySchema.Directory.QueryAsync(tx, step, ct)
                .ConfigureAwait(false);
            if (page.Items.Count == 0)
            {
                scanCursor = null;
                break;
            }

            if (page.Items[0].TeamId.ToString().Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(page.Items[0]);
            }

            scanCursor = page.NextCursor;
            if (scanCursor is null)
            {
                break;
            }
        }

        return new Page<RoleTeamView>(matches, matches.Count == effectiveLimit ? scanCursor : null);
    }
}
