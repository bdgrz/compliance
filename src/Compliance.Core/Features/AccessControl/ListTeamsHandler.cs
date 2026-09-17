using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class ListTeamsHandler(ITeamDirectoryReader directory) : IRequestHandler<ListTeams, Page<TeamView>>
{
    public async ValueTask<Result<Page<TeamView>>> HandleAsync(IRequestContext<ListTeams> context, CancellationToken ct)
    {
        var request = context.Request;
        var page = await directory.ListAsync(
            request.TenantId,
            request.Limit,
            request.Cursor,
            ListRequestNormalization.NormalizeSearch(request.Search),
            ListRequestNormalization.IsDescending(request.Sort),
            ct);
        return Result<Page<TeamView>>.Success(page);
    }
}
