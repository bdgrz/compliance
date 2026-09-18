using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The shared role-team-directory schema — the same <see cref="TeamRoleAssigned" />/
///     <see cref="TeamRoleRemoved" /> events as <c>rbac-team-roles</c>, materialized keyed by role
///     first instead of by team, so a role's detail page can list the teams holding it without a
///     substring-search fallback over an unrelated index.
/// </summary>
static class RoleTeamDirectorySchema
{
    public static readonly KvDirectoryIndex<RoleTeamView> ByRole = new(
        "by_role", 1, static view => [view.RoleId.ToString(), view.TeamId.ToString()]);

    public static readonly KvDirectory<RoleTeamView, (Uuid RoleId, Uuid TeamId)> Directory = new(
        "role-teams",
        ComplianceCoreJsonContext.Default.RoleTeamView,
        static view => (view.RoleId, view.TeamId),
        static identity => [identity.RoleId.ToString(), identity.TeamId.ToString()],
        [ByRole]);
}
