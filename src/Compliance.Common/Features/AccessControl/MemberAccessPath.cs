using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed record MemberAccessPath(Uuid TeamId, string TeamName,
    Uuid RoleId, string RoleName, IReadOnlyList<string> Permissions);
