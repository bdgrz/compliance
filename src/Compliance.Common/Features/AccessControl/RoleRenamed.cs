using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.rbac.role.renamed", 1)]
public sealed record RoleRenamed(Uuid TenantId, Uuid RoleId, string Name) : DomainEvent;
