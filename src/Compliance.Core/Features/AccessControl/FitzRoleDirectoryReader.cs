using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Reads the role directory directly from Fitz KV, independent of any projector's workload
///     scope — same direct-read pattern as <see cref="FitzTeamDirectoryReader" />.
/// </summary>
sealed class FitzRoleDirectoryReader(IKvClient client)
    : FitzKvProjectionStore(client, RoleDirectoryKeys.Route, "RoleDirectory"),
      IRoleDirectoryProjection,
      IRoleDirectoryReader
{
    const int DefaultLimit = 50;
    const int MaxLimit = 200;

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case RoleDefined defined:
                await RoleDirectorySchema.Directory.InsertAsync(
                    Transaction, new RoleView(defined.RoleId, defined.Name), ct).ConfigureAwait(false);
                break;
            case RoleDeleted deleted:
                await RoleDirectorySchema.Directory.DeleteAsync(Transaction, deleted.RoleId, ct).ConfigureAwait(false);
                break;
        }
    }

    public async ValueTask<RoleView?> GetAsync(Uuid tenantId, Uuid roleId, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await RoleDirectorySchema.Directory.GetAsync(tx, roleId, ct).ConfigureAwait(false);
    }

    public async ValueTask<Page<RoleView>> ListAsync(
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
            var query = RoleDirectorySchema.ByName.Query().After(cursor);
            if (limit is not null)
            {
                query = query.Take(limit.Value);
            }

            if (descending)
            {
                query = query.Descending();
            }

            return await RoleDirectorySchema.Directory.QueryAsync(tx, query, ct).ConfigureAwait(false);
        }

        return await SearchAsync(tx, limit, cursor, search, descending, ct).ConfigureAwait(false);
    }

    // Same reasoning as FitzTeamDirectoryReader.SearchAsync — see
    // design-api-contracts-hide-impl-strategy: Search means substring, not "whatever the index can
    // serve directly."
    static async ValueTask<Page<RoleView>> SearchAsync(
        IKvTransaction tx, int? limit, string? cursor, string search, bool descending, CancellationToken ct)
    {
        var effectiveLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var matches = new List<RoleView>();
        var scanCursor = cursor;
        while (matches.Count < effectiveLimit)
        {
            var step = RoleDirectorySchema.ByName.Query().Take(1).After(scanCursor);
            if (descending)
            {
                step = step.Descending();
            }

            var page = await RoleDirectorySchema.Directory.QueryAsync(tx, step, ct).ConfigureAwait(false);
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

        return new Page<RoleView>(matches, matches.Count == effectiveLimit ? scanCursor : null);
    }
}
