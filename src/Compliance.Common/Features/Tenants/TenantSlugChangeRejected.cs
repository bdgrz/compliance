using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant-slug.change-rejected", 1)]
public sealed record TenantSlugChangeRejected(Uuid TenantId, string OldSlug, string NewSlug) : DomainEvent;
