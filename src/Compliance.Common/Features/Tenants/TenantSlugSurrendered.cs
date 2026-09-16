using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant-slug.surrendered", 1)]
public sealed record TenantSlugSurrendered(Uuid TenantId, string Slug) : DomainEvent;
