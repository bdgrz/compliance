using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>The shared team-directory schema, used by both the read and write sides.</summary>
static class TeamDirectorySchema
{
    public static readonly KvDirectoryIndex<TeamView> ByName = new(
        "by_name", 1, static team => [Normalize(team.Name)]);

    public static readonly KvDirectory<TeamView, Uuid> Directory = new(
        "teams",
        ComplianceCoreJsonContext.Default.TeamView,
        static team => team.TeamId,
        static teamId => [teamId.ToString()],
        [ByName]);

    public static string Normalize(string value) => value.ToUpperInvariant();
}
