using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed record MemberAccessEdge(Uuid TeamId, Uuid RoleId,
    IReadOnlyList<string> Permissions);
