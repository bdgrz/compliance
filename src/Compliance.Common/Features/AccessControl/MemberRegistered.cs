using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.rbac.member.registered", 1)]
public sealed record MemberRegistered(Uuid TenantId, Uuid MemberId, Uuid UserId) : DomainEvent;
