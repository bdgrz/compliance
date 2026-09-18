using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class ListRoleTeamsHandler(IRoleTeamDirectoryReader directory)
    : IRequestHandler<ListRoleTeams, Page<RoleTeamView>>
{
    public async ValueTask<Result<Page<RoleTeamView>>> HandleAsync(
        IRequestContext<ListRoleTeams> context, CancellationToken ct)
    {
        var request = context.Request;
        var page = await directory.ListAsync(
            request.TenantId,
            request.RoleId,
            request.Limit,
            request.Cursor,
            ListRequestNormalization.NormalizeSearch(request.Search),
            ListRequestNormalization.IsDescending(request.Sort),
            ct);
        return Result<Page<RoleTeamView>>.Success(page);
    }
}
