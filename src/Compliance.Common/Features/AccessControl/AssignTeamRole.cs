using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.rbac.team-role.assign", 1)]
public sealed record AssignTeamRole(Uuid TenantId, Uuid TeamId, Uuid RoleId) : IRequest, IRbacManagementRequest;
