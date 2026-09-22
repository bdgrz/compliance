using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class ListRolesHandler(IRoleDirectoryReader directory) : IRequestHandler<ListRoles, Page<RoleView>>
{
    public async ValueTask<Result<Page<RoleView>>> HandleAsync(IRequestContext<ListRoles> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<RoleView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The role list limit must be between 1 and 200."));
        Page<RoleView> page;
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
            return Result<Page<RoleView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The role cursor is invalid."));
        }
        return Result<Page<RoleView>>.Success(page);
    }
}
