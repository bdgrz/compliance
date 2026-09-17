using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant-slug-surrender.confirmed", 1)]
public sealed record TenantSlugSurrenderConfirmed(Uuid TenantId, string Slug) : DomainEvent;
