using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.rbac.team.defined", 1)]
public sealed record TeamDefined(Uuid TenantId, Uuid TeamId, string Name) : DomainEvent;
