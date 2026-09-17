using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.rbac.role-permission.assigned", 1)]
public sealed record RolePermissionAssigned(Uuid TenantId, Uuid RoleId, string Permission) : DomainEvent;
