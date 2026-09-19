using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Assigns a role to a team, adding it if it is not already assigned.</summary>
[Discriminator("bdgrz.rbac.team-role.assign", 1)]
public sealed record AssignTeamRole(Uuid TenantId, Uuid TeamId, Uuid RoleId)
    : IRequest, IRbacManagementRequest, ICallable;
