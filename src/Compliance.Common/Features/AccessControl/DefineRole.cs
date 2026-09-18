using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Defines a role within a tenant, creating it if it does not already exist.</summary>
[Discriminator("bdgrz.rbac.role.define", 1)]
public sealed record DefineRole(Uuid TenantId, Uuid RoleId, string Name)
    : IRequest, IRbacManagementRequest, ICallable;
