using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Reads the role-team directory directly from Fitz KV, independent of any projector's workload
///     scope — same direct-read pattern as <see cref="FitzTeamMemberDirectoryReader" />.
/// </summary>
sealed class FitzRoleTeamDirectoryReader(IKvClient client) : IRoleTeamDirectoryReader
{
    const int DefaultLimit = 50;
    const int MaxLimit = 200;

    public ValueTask<Page<RoleTeamView>> ListAsync(
        Uuid tenantId,
        Uuid roleId,
        int? limit,
        string? cursor,
        string? search,
        bool descending,
        CancellationToken ct = default)
    {
        var route = RoleTeamDirectoryKeys.Route(tenantId.ToString());
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

            return RoleTeamDirectorySchema.Directory.QueryAsync(client, route, query, ct);
        }

        return SearchAsync(route, roleId, limit, cursor, search, descending, ct);
    }

    // Same reasoning as FitzTeamMemberDirectoryReader.SearchAsync — see
    // design-api-contracts-hide-impl-strategy: Search means substring, not "whatever the index can
    // serve directly."
    async ValueTask<Page<RoleTeamView>> SearchAsync(
        string route, Uuid roleId, int? limit, string? cursor, string search, bool descending, CancellationToken ct)
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

            var page = await RoleTeamDirectorySchema.Directory.QueryAsync(client, route, step, ct)
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
