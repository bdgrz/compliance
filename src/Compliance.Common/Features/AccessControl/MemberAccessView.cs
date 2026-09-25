using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed record MemberAccessView(Uuid TenantId, Uuid UserId, Uuid MemberId,
    IReadOnlyList<MemberAccessPath> Paths, IReadOnlyList<string> EffectivePermissions);
