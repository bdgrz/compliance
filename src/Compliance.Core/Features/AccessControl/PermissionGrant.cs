using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed record PermissionGrant(Uuid MemberId, string Permission);
