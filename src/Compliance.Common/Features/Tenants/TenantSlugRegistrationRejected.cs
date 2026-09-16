using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant-slug.registration-rejected", 1)]
public sealed record TenantSlugRegistrationRejected(Uuid TenantId, string Slug) : DomainEvent;
