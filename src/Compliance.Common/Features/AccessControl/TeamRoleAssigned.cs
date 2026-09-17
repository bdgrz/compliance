using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.rbac.team-role.assigned", 1)]
public sealed record TeamRoleAssigned(Uuid TenantId, Uuid TeamId, Uuid RoleId) : DomainEvent;
