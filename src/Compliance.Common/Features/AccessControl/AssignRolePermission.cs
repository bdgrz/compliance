using System.Diagnostics.CodeAnalysis;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "AssignRolePermission uses the canonical RBAC relationship term.")]
[Discriminator("bdgrz.rbac.role-permission.assign", 1)]
public sealed record AssignRolePermission(Uuid TenantId, Uuid RoleId, string Permission)
    : IRequest, IRbacManagementRequest;
