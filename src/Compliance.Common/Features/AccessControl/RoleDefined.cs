using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.rbac.role.defined", 1)]
public sealed record RoleDefined(Uuid TenantId, Uuid RoleId, string Name) : DomainEvent;
