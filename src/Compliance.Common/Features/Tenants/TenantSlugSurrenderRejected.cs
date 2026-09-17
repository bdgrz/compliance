using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant-slug.surrender-rejected", 1)]
public sealed record TenantSlugSurrenderRejected(Uuid TenantId, string Slug) : DomainEvent;
