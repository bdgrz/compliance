using Cntryl.Fitz.Extensions;
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
        if (request.Limit is < 1 or > 200)
            return Result<Page<TeamMemberView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The team member list limit must be between 1 and 200."));
        Page<TeamMemberView> page;
        try
        {
            page = await directory.ListAsync(
                request.TenantId,
                request.TeamId,
                request.Limit ?? 50,
                request.Cursor,
                ListRequestNormalization.NormalizeSearch(request.Search),
                ListRequestNormalization.IsDescending(request.Sort),
                ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<TeamMemberView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The team member cursor is invalid."));
        }
        return page.Items.Any(item => item.TeamId != request.TeamId)
            ? Result<Page<TeamMemberView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The team members were not found."))
            : Result<Page<TeamMemberView>>.Success(page);
    }
}
