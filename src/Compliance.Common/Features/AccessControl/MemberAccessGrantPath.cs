namespace Bdgrz.Compliance.Features.AccessControl;

public sealed record MemberAccessGrantPath(AccessGrantView Grant, string RoleName,
    IReadOnlyList<string> Permissions, bool IsEffective);
