using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Lists a team's members by member ID. <c>Limit</c> defaults to 50 and must be between 1 and
///     200; <c>Cursor</c> resumes a previous page; <c>Search</c> filters by a case-insensitive
///     member-ID substring; <c>Sort</c> is <c>"member_id"</c> or <c>"member_id:desc"</c> — members
///     have exactly one index, so anything containing <c>"desc"</c> reverses it.
/// </summary>
[Discriminator("bdgrz.rbac.team-member.list", 1)]
public sealed record ListTeamMembers(
    Uuid TenantId,
    Uuid TeamId,
    int? Limit = null,
    string? Cursor = null,
    string? Search = null,
    string? Sort = null) : IRequest<Page<TeamMemberView>>, ITenantAccessRequest, ICallable;
