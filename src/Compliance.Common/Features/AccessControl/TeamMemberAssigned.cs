using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.rbac.team-member.assigned", 1)]
public sealed record TeamMemberAssigned(Uuid TenantId, Uuid TeamId, Uuid MemberId) : DomainEvent;
