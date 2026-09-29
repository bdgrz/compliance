using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.rbac.member.suspended", 1)]
public sealed record MemberSuspended(Uuid TenantId, Uuid MemberId, Uuid UserId,
    Uuid SuspendedByMemberId, string SuspendedByDisplay, DateTimeOffset SuspendedAt,
    string Reason) : DomainEvent;
