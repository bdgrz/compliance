using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Lists a role's permissions. <c>Limit</c> defaults to 50 and must be between 1 and 200;
///     <c>Cursor</c> resumes a previous page; <c>Search</c> filters by a case-insensitive
///     permission substring; <c>Sort</c> is <c>"permission"</c> or <c>"permission:desc"</c> —
///     permissions have exactly one index, so anything containing <c>"desc"</c> reverses it.
/// </summary>
[Discriminator("bdgrz.rbac.role-permission.list", 1)]
public sealed record ListRolePermissions(
    Uuid TenantId,
    Uuid RoleId,
    int? Limit = null,
    string? Cursor = null,
    string? Search = null,
    string? Sort = null) : IRequest<Page<RolePermissionView>>, ITenantAccessRequest, ICallable;
