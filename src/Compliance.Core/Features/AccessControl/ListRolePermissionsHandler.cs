using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class ListRolePermissionsHandler(IRolePermissionDirectoryReader directory)
    : IRequestHandler<ListRolePermissions, Page<RolePermissionView>>
{
    public async ValueTask<Result<Page<RolePermissionView>>> HandleAsync(
        IRequestContext<ListRolePermissions> context, CancellationToken ct)
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
        return Result<Page<RolePermissionView>>.Success(page);
    }
}
