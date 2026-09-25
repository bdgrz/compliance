using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.member.access.get", 1)]
public sealed record GetMemberAccess(Uuid TenantId, Uuid UserId,
    string? ExpectedBuiltInRole = null)
    : IRequest<MemberAccessView>, IRbacManagementRequest, ICallable;
