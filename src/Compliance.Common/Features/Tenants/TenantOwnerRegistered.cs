using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant-owner.registered", 1)]
public sealed record TenantOwnerRegistered(Uuid TenantId, Uuid UserId) : DomainEvent;
