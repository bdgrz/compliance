using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed record MemberAccessPath(Uuid TeamId, string TeamName,
    Uuid RoleId, string RoleName, IReadOnlyList<string> Permissions);

public sealed record MemberAccessView(Uuid TenantId, Uuid UserId, Uuid MemberId,
    IReadOnlyList<MemberAccessPath> Paths, IReadOnlyList<string> EffectivePermissions);

[Discriminator("bdgrz.member.access.get", 1)]
public sealed record GetMemberAccess(Uuid TenantId, Uuid UserId,
    string? ExpectedBuiltInRole = null)
    : IRequest<MemberAccessView>, IRbacManagementRequest, ICallable;
