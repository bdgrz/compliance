using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.rbac.member.deprovisioned", 1)]
public sealed record MemberDeprovisioned(Uuid TenantId, Uuid MemberId, Uuid UserId,
    Uuid DeprovisionedByMemberId, string DeprovisionedByDisplay, DateTimeOffset DeprovisionedAt,
    string Reason) : DomainEvent;
