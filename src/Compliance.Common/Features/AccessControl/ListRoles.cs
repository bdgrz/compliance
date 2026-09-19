using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Lists a tenant's roles by name. <c>Limit</c> defaults to 50 and must be between 1 and 200;
///     <c>Cursor</c> resumes a previous page; <c>Search</c> filters by a case-insensitive name
///     substring; <c>Sort</c> is <c>"name"</c> or <c>"name:desc"</c> — roles have exactly one index,
///     so anything containing <c>"desc"</c> reverses it.
/// </summary>
[Discriminator("bdgrz.rbac.role.list", 1)]
public sealed record ListRoles(
    Uuid TenantId,
    int? Limit = null,
    string? Cursor = null,
    string? Search = null,
    string? Sort = null) : IRequest<Page<RoleView>>, ITenantAccessRequest, ICallable;
