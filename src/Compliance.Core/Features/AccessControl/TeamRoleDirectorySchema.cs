using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The team-role-directory schema — the same <see cref="TeamRoleAssigned" />/
///     <see cref="TeamRoleRemoved" /> events as <see cref="RoleTeamDirectorySchema" />, materialized
///     keyed by team first, so a team's page can list its roles in one read instead of one request
///     per role.
/// </summary>
static class TeamRoleDirectorySchema
{
    public static readonly KvDirectoryIndex<TeamRoleView> ByTeam = new(
        "by_team", 1, static view => [view.TeamId.ToString(), view.RoleId.ToString()]);

    public static readonly KvDirectory<TeamRoleView, (Uuid TeamId, Uuid RoleId)> Directory = new(
        "team-roles",
        ComplianceCoreJsonContext.Default.TeamRoleView,
        static view => (view.TeamId, view.RoleId),
        static identity => [identity.TeamId.ToString(), identity.RoleId.ToString()],
        [ByTeam]);
}
