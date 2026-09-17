using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant-slug.confirmed", 1)]
public sealed record TenantSlugConfirmed(Uuid TenantId, string Slug) : DomainEvent;
