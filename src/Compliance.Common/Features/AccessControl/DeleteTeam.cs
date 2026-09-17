using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Deletes a team from a tenant. Built-in teams cannot be deleted.</summary>
[Discriminator("bdgrz.rbac.team.delete", 1)]
public sealed record DeleteTeam(Uuid TenantId, Uuid TeamId) : IRequest, IRbacManagementRequest, ICallable;
