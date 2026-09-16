using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.rbac.team.define", 1)]
public sealed record DefineTeam(Uuid TenantId, Uuid TeamId, string Name) : IRequest;
