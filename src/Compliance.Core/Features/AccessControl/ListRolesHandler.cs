using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class ListRolesHandler(IRoleDirectoryReader directory) : IRequestHandler<ListRoles, Page<RoleView>>
{
    public async ValueTask<Result<Page<RoleView>>> HandleAsync(IRequestContext<ListRoles> context, CancellationToken ct)
    {
        var request = context.Request;
        var page = await directory.ListAsync(
            request.TenantId,
            request.Limit,
            request.Cursor,
            ListRequestNormalization.NormalizeSearch(request.Search),
            ListRequestNormalization.IsDescending(request.Sort),
            ct);
        return Result<Page<RoleView>>.Success(page);
    }
}
