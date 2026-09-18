using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Lists the teams a role is assigned to. <c>Limit</c> defaults to 50 and must be between 1 and
///     200; <c>Cursor</c> resumes a previous page; <c>Search</c> filters by a case-insensitive
///     team-ID substring; <c>Sort</c> is <c>"team_id"</c> or <c>"team_id:desc"</c> — teams have
///     exactly one index, so anything containing <c>"desc"</c> reverses it.
/// </summary>
[Discriminator("bdgrz.rbac.role-team.list", 1)]
public sealed record ListRoleTeams(
    Uuid TenantId,
    Uuid RoleId,
    int? Limit = null,
    string? Cursor = null,
    string? Search = null,
    string? Sort = null) : IRequest<Page<RoleTeamView>>, ITenantAccessRequest, ICallable;
