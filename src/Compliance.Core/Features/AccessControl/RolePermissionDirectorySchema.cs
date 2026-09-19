using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>The shared role-permission-directory schema, used by both the read and write sides.</summary>
static class RolePermissionDirectorySchema
{
    public static readonly KvDirectoryIndex<RolePermissionView> ByRole = new(
        "by_role", 1, static view => [view.RoleId.ToString(), view.Permission]);

    public static readonly KvDirectory<RolePermissionView, (Uuid RoleId, string Permission)> Directory = new(
        "role-permissions",
        ComplianceCoreJsonContext.Default.RolePermissionView,
        static view => (view.RoleId, view.Permission),
        static identity => [identity.RoleId.ToString(), identity.Permission],
        [ByRole]);
}
