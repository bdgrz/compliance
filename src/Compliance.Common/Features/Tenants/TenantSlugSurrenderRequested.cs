using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant-slug.surrender-requested", 1)]
public sealed record TenantSlugSurrenderRequested(Uuid TenantId, string Slug) : DomainEvent;
