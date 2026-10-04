using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.rbac.member.deprovision_cleanup_completed", 1)]
public sealed record MemberDeprovisionCleanupCompleted(Uuid TenantId, Uuid MemberId,
    DateTimeOffset CompletedAt) : DomainEvent;
