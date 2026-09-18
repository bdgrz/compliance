using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Serves the "TeamDirectory" projector: writes join its batch transaction, and query-side
///     reads open their own read-only transaction on the resource it writes for the given tenant.
/// </summary>
sealed class FitzTeamDirectoryReader(IKvClient client)
    : FitzKvProjectionStore(client, TeamDirectoryKeys.Route, "TeamDirectory"),
      ITeamDirectoryProjection,
      ITeamDirectoryReader
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case TeamDefined defined:
                await TeamDirectorySchema.Directory.InsertAsync(
                    Transaction, new TeamView(defined.TeamId, defined.Name), ct).ConfigureAwait(false);
                break;
            case TeamDeleted deleted:
                await TeamDirectorySchema.Directory.DeleteAsync(Transaction, deleted.TeamId, ct).ConfigureAwait(false);
                break;
        }
    }

    public async ValueTask<TeamView?> GetAsync(Uuid tenantId, Uuid teamId, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await TeamDirectorySchema.Directory.GetAsync(tx, teamId, ct).ConfigureAwait(false);
    }

    public async ValueTask<Page<TeamView>> ListAsync(
        Uuid tenantId,
        int? limit,
        string? cursor,
        string? search,
        bool descending,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
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

            return await TeamDirectorySchema.Directory.QueryAsync(tx, query, ct).ConfigureAwait(false);
        }

        return await SearchAsync(tx, limit, cursor, search, descending, ct).ConfigureAwait(false);
    }

    // KvDirectory's declared indexes only support prefix range scans, not substring matching, so a
    // real substring search walks the name-ordered index one entry at a time (each with its own
    // precise resume cursor, unlike a bulk-fetched page) and filters here — more round trips than
    // the no-search path, acceptable for the small per-tenant team counts this app has. Search means
    // substring, the same as before the KvDirectory 1.3.0 migration; see the
    // design-api-contracts-hide-impl-strategy note — the contract does not narrow just because the
    // storage layer's native query shape changed underneath it.
    static async ValueTask<Page<TeamView>> SearchAsync(
        IKvTransaction tx, int? limit, string? cursor, string search, bool descending, CancellationToken ct)
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

            var page = await TeamDirectorySchema.Directory.QueryAsync(tx, step, ct).ConfigureAwait(false);
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
