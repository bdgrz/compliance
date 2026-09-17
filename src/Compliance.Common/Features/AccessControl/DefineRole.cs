using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.rbac.role.define", 1)]
public sealed record DefineRole(Uuid TenantId, Uuid RoleId, string Name) : IRequest, IRbacManagementRequest;
