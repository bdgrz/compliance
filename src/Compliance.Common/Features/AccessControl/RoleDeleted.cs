using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.rbac.role.deleted", 1)]
public sealed record RoleDeleted(Uuid TenantId, Uuid RoleId) : DomainEvent;
