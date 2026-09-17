using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Reads the team directory directly from Fitz KV, independent of any projector's workload
///     scope — mirrors <see cref="FitzPermissionAuthorizer" />'s direct-read pattern rather than the
///     <see cref="IProjectionStore" />-typed write side, which only applies during a projector pass.
/// </summary>
sealed class FitzTeamDirectoryReader(IKvClient client) : ITeamDirectoryReader
{
    const int DefaultLimit = 50;
    const int MaxLimit = 200;

    public ValueTask<TeamView?> GetAsync(Uuid tenantId, Uuid teamId, CancellationToken ct = default) =>
        TeamDirectorySchema.Directory.GetAsync(client, TeamDirectoryKeys.Route(tenantId.ToString()), teamId, ct);

    public ValueTask<Page<TeamView>> ListAsync(
        Uuid tenantId,
        int? limit,
        string? cursor,
        string? search,
        bool descending,
        CancellationToken ct = default)
    {
        var route = TeamDirectoryKeys.Route(tenantId.ToString());
        if (search is null)
        {
            var query = TeamDirectorySchema.ByName.Query().After(cursor);
            if (limit is not null)
            {
                query = query.Take(limit.Value);
            }

            if (descending)
            {
                query = query.Descending();
            }

            return TeamDirectorySchema.Directory.QueryAsync(client, route, query, ct);
        }

        return SearchAsync(route, limit, cursor, search, descending, ct);
    }

    // KvDirectory's declared indexes only support prefix range scans, not substring matching, so a
    // real substring search walks the name-ordered index one entry at a time (each with its own
    // precise resume cursor, unlike a bulk-fetched page) and filters here — more round trips than
    // the no-search path, acceptable for the small per-tenant team counts this app has. Search means
    // substring, the same as before the KvDirectory 1.3.0 migration; see the
    // design-api-contracts-hide-impl-strategy note — the contract does not narrow just because the
    // storage layer's native query shape changed underneath it.
    async ValueTask<Page<TeamView>> SearchAsync(
        string route, int? limit, string? cursor, string search, bool descending, CancellationToken ct)
    {
        var effectiveLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var matches = new List<TeamView>();
        var scanCursor = cursor;
        while (matches.Count < effectiveLimit)
        {
            var step = TeamDirectorySchema.ByName.Query().Take(1).After(scanCursor);
            if (descending)
            {
                step = step.Descending();
            }

            var page = await TeamDirectorySchema.Directory.QueryAsync(client, route, step, ct).ConfigureAwait(false);
            if (page.Items.Count == 0)
            {
                scanCursor = null;
                break;
            }

            if (page.Items[0].Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(page.Items[0]);
            }

            scanCursor = page.NextCursor;
            if (scanCursor is null)
            {
                break;
            }
        }

        return new Page<TeamView>(matches, matches.Count == effectiveLimit ? scanCursor : null);
    }
}
