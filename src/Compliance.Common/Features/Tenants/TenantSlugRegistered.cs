using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant-slug.registered", 1)]
public sealed record TenantSlugRegistered(Uuid TenantId, string Slug) : DomainEvent;
