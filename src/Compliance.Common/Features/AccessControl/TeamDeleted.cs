using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.rbac.team.deleted", 1)]
public sealed record TeamDeleted(Uuid TenantId, Uuid TeamId) : DomainEvent;
