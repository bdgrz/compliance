using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Removes a member from a team.</summary>
[Discriminator("bdgrz.rbac.team-member.remove", 1)]
public sealed record RemoveTeamMember(Uuid TenantId, Uuid TeamId, Uuid MemberId)
    : IRequest, IRbacManagementRequest, ICallable;
