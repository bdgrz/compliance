using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant.reactivated", 1)]
public sealed record TenantReactivated(Uuid TenantId, Uuid OperatorUserId) : DomainEvent;
