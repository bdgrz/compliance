using System.Diagnostics.CodeAnalysis;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Removes a permission from a role.</summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "RemoveRolePermission uses the canonical RBAC relationship term.")]
[Discriminator("bdgrz.rbac.role-permission.remove", 1)]
public sealed record RemoveRolePermission(Uuid TenantId, Uuid RoleId, string Permission)
    : IRequest, IRbacManagementRequest, ICallable;
