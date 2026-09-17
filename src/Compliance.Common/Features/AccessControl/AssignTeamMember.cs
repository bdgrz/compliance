using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Assigns a member to a team, adding them if they are not already on it.</summary>
[Discriminator("bdgrz.rbac.team-member.assign", 1)]
public sealed record AssignTeamMember(Uuid TenantId, Uuid TeamId, Uuid MemberId)
    : IRequest, IRbacManagementRequest, ICallable;
