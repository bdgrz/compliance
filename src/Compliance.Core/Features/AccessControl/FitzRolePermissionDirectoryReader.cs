using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Reads the role-permission directory directly from Fitz KV, independent of any projector's
///     workload scope — same direct-read pattern as <see cref="FitzTeamMemberDirectoryReader" />.
/// </summary>
sealed class FitzRolePermissionDirectoryReader(IKvClient client)
    : FitzKvProjectionStore(client, RolePermissionDirectoryKeys.Route, "RolePermissionDirectory"),
      IRolePermissionDirectoryProjection,
      IRolePermissionDirectoryReader
{
    const int DefaultLimit = 50;
    const int MaxLimit = 200;

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case RolePermissionAssigned assigned:
                await RolePermissionDirectorySchema.Directory.InsertAsync(
                    Transaction, new RolePermissionView(assigned.RoleId, assigned.Permission), ct).ConfigureAwait(false);
                break;
            case RolePermissionRemoved removed:
                await RolePermissionDirectorySchema.Directory.DeleteAsync(
                    Transaction, (removed.RoleId, removed.Permission), ct).ConfigureAwait(false);
                break;
        }
    }

    public async ValueTask<Page<RolePermissionView>> ListAsync(
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
            var query = RolePermissionDirectorySchema.ByRole.Query().WithPrefix(roleId.ToString()).After(cursor);
            if (limit is not null)
            {
                query = query.Take(limit.Value);
            }

            if (descending)
            {
                query = query.Descending();
            }

            return await RolePermissionDirectorySchema.Directory.QueryAsync(tx, query, ct).ConfigureAwait(false);
        }

        return await SearchAsync(tx, roleId, limit, cursor, search, descending, ct).ConfigureAwait(false);
    }

    // Same reasoning as FitzTeamMemberDirectoryReader.SearchAsync — see
    // design-api-contracts-hide-impl-strategy: Search means substring, not "whatever the index can
    // serve directly."
    static async ValueTask<Page<RolePermissionView>> SearchAsync(
        IKvTransaction tx, Uuid roleId, int? limit, string? cursor, string search, bool descending, CancellationToken ct)
    {
        var effectiveLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var matches = new List<RolePermissionView>();
        var scanCursor = cursor;
        while (matches.Count < effectiveLimit)
        {
            var step = RolePermissionDirectorySchema.ByRole.Query().WithPrefix(roleId.ToString()).Take(1)
                .After(scanCursor);
            if (descending)
            {
                step = step.Descending();
            }

            var page = await RolePermissionDirectorySchema.Directory.QueryAsync(tx, step, ct)
                .ConfigureAwait(false);
            if (page.Items.Count == 0)
            {
                scanCursor = null;
                break;
            }

            if (page.Items[0].Permission.Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(page.Items[0]);
            }

            scanCursor = page.NextCursor;
            if (scanCursor is null)
            {
                break;
            }
        }

        return new Page<RolePermissionView>(matches, matches.Count == effectiveLimit ? scanCursor : null);
    }
}
