using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class ListTeamRolesHandler(ITeamRoleDirectoryReader directory)
    : IRequestHandler<ListTeamRoles, Page<TeamRoleView>>
{
    public async ValueTask<Result<Page<TeamRoleView>>> HandleAsync(
        IRequestContext<ListTeamRoles> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<TeamRoleView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The team role list limit must be between 1 and 200."));
        Page<TeamRoleView> page;
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
            return Result<Page<TeamRoleView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The team role cursor is invalid."));
        }
        return page.Items.Any(item => item.TeamId != request.TeamId)
            ? Result<Page<TeamRoleView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The team roles were not found."))
            : Result<Page<TeamRoleView>>.Success(page);
    }
}
