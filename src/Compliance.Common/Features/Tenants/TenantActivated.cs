using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant.activated", 1)]
public sealed record TenantActivated(Uuid TenantId, Uuid FirstAdministratorUserId) : DomainEvent;
