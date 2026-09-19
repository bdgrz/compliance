using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant-slug.changed", 1)]
public sealed record TenantSlugChanged(Uuid TenantId, string OldSlug, string NewSlug) : DomainEvent;
