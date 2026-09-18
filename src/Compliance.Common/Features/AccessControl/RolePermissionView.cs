using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed record RolePermissionView(Uuid RoleId, string Permission);
