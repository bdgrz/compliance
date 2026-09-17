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
        var query = TeamDirectorySchema.ByName.Query().After(cursor);
        if (limit is not null)
        {
            query = query.Take(limit.Value);
        }

        if (descending)
        {
            query = query.Descending();
        }

        if (search is not null)
        {
            query = query.WithPrefix(TeamDirectorySchema.Normalize(search));
        }

        return TeamDirectorySchema.Directory.QueryAsync(client, TeamDirectoryKeys.Route(tenantId.ToString()), query, ct);
    }
}
