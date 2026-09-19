using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.rbac.role-permission.removed", 1)]
public sealed record RolePermissionRemoved(Uuid TenantId, Uuid RoleId, string Permission) : DomainEvent;
