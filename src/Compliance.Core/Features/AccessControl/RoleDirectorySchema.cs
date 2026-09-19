using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>The shared role-directory schema, used by both the read and write sides.</summary>
static class RoleDirectorySchema
{
    public static readonly KvDirectoryIndex<RoleView> ByName = new(
        "by_name", 1, static role => [Normalize(role.Name)]);

    public static readonly KvDirectory<RoleView, Uuid> Directory = new(
        "roles",
        ComplianceCoreJsonContext.Default.RoleView,
        static role => role.RoleId,
        static roleId => [roleId.ToString()],
        [ByName]);

    public static string Normalize(string value) => value.ToUpperInvariant();
}
