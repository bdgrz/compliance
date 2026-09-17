using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant-slug.rejected", 1)]
public sealed record TenantSlugRejected(Uuid TenantId, string Slug) : DomainEvent;
