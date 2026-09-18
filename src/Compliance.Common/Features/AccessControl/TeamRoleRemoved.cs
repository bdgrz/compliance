using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.rbac.team-role.removed", 1)]
public sealed record TeamRoleRemoved(Uuid TenantId, Uuid TeamId, Uuid RoleId) : DomainEvent;
