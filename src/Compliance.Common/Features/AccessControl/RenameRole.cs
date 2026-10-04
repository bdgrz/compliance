using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.rbac.role.rename", 1)]
public sealed record RenameRole(Uuid TenantId, Uuid RoleId, string Name)
    : IRequest, IRbacManagementRequest, ICallable;
