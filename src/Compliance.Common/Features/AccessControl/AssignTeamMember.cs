using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.rbac.team-member.assign", 1)]
public sealed record AssignTeamMember(Uuid TenantId, Uuid TeamId, Uuid MemberId) : IRequest;
