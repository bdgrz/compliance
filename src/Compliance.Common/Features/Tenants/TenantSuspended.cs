using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant.suspended", 1)]
public sealed record TenantSuspended(Uuid TenantId, Uuid OperatorUserId) : DomainEvent;
