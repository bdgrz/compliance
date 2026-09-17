using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Defines a team within a tenant, creating it if it does not already exist.</summary>
[Discriminator("bdgrz.rbac.team.define", 1)]
public sealed record DefineTeam(Uuid TenantId, Uuid TeamId, string Name) : IRequest, IRbacManagementRequest, ICallable;
