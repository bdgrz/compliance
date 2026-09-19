using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Removes a role from a team.</summary>
[Discriminator("bdgrz.rbac.team-role.remove", 1)]
public sealed record RemoveTeamRole(Uuid TenantId, Uuid TeamId, Uuid RoleId)
    : IRequest, IRbacManagementRequest, ICallable;
