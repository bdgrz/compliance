using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Reads one role by id.</summary>
[Discriminator("bdgrz.rbac.role.get", 1)]
public sealed record GetRole(Uuid TenantId, Uuid RoleId) : IRequest<RoleView>, ITenantAccessRequest, ICallable;
