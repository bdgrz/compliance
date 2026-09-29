using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.rbac.member.reinstated", 1)]
public sealed record MemberReinstated(Uuid TenantId, Uuid MemberId, Uuid UserId,
    string Affiliation, Uuid ReinstatedByMemberId, string ReinstatedByDisplay,
    DateTimeOffset ReinstatedAt) : DomainEvent;
