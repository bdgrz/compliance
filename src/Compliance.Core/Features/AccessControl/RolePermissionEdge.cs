using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed record RolePermissionEdge(Uuid RoleId, string Permission);
