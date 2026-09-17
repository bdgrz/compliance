using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class ListTeamMembersHandler(ITeamMemberDirectoryReader directory)
    : IRequestHandler<ListTeamMembers, Page<TeamMemberView>>
{
    public async ValueTask<Result<Page<TeamMemberView>>> HandleAsync(
        IRequestContext<ListTeamMembers> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var page = await directory.ListAsync(
            request.TenantId,
            request.TeamId,
            request.Limit,
            request.Cursor,
            ListRequestNormalization.NormalizeSearch(request.Search),
            ListRequestNormalization.IsDescending(request.Sort),
            ct);
        return Result<Page<TeamMemberView>>.Success(page);
    }
}
