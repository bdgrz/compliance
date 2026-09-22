using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class ListTeamsHandler(ITeamDirectoryReader directory) : IRequestHandler<ListTeams, Page<TeamView>>
{
    public async ValueTask<Result<Page<TeamView>>> HandleAsync(IRequestContext<ListTeams> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<TeamView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The team list limit must be between 1 and 200."));
        Page<TeamView> page;
        try
        {
            page = await directory.ListAsync(
                request.TenantId,
                request.Limit ?? 50,
                request.Cursor,
                ListRequestNormalization.NormalizeSearch(request.Search),
                ListRequestNormalization.IsDescending(request.Sort),
                ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<TeamView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The team cursor is invalid."));
        }
        return Result<Page<TeamView>>.Success(page);
    }
}
