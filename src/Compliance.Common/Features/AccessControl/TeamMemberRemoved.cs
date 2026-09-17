using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.rbac.team-member.removed", 1)]
public sealed record TeamMemberRemoved(Uuid TenantId, Uuid TeamId, Uuid MemberId) : DomainEvent;
