using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class ListRolePermissionsHandler(IRolePermissionDirectoryReader directory)
    : IRequestHandler<ListRolePermissions, Page<RolePermissionView>>
{
    public async ValueTask<Result<Page<RolePermissionView>>> HandleAsync(
        IRequestContext<ListRolePermissions> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<RolePermissionView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The role permission list limit must be between 1 and 200."));
        Page<RolePermissionView> page;
        try
        {
            page = await directory.ListAsync(
                request.TenantId,
                request.RoleId,
                request.Limit ?? 50,
                request.Cursor,
                ListRequestNormalization.NormalizeSearch(request.Search),
                ListRequestNormalization.IsDescending(request.Sort),
                ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<RolePermissionView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The role permission cursor is invalid."));
        }
        return page.Items.Any(item => item.RoleId != request.RoleId)
            ? Result<Page<RolePermissionView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The role permissions were not found."))
            : Result<Page<RolePermissionView>>.Success(page);
    }
}
