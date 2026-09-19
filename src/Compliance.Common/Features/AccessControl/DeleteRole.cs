using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Deletes a role from a tenant. Built-in roles cannot be deleted.</summary>
[Discriminator("bdgrz.rbac.role.delete", 1)]
public sealed record DeleteRole(Uuid TenantId, Uuid RoleId) : IRequest, IRbacManagementRequest, ICallable;
