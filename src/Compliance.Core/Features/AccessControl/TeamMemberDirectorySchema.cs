using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>The shared team-member-directory schema, used by both the read and write sides.</summary>
static class TeamMemberDirectorySchema
{
    public static readonly KvDirectoryIndex<TeamMemberView> ByTeam = new(
        "by_team", 1, static view => [view.TeamId.ToString(), view.MemberId.ToString()]);

    public static readonly KvDirectory<TeamMemberView, (Uuid TeamId, Uuid MemberId)> Directory = new(
        "team-members",
        ComplianceCoreJsonContext.Default.TeamMemberView,
        static view => (view.TeamId, view.MemberId),
        static identity => [identity.TeamId.ToString(), identity.MemberId.ToString()],
        [ByTeam]);
}
