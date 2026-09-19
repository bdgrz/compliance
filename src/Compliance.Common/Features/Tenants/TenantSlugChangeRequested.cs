using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant-slug.change-requested", 1)]
public sealed record TenantSlugChangeRequested(Uuid TenantId, string OldSlug, string NewSlug) : DomainEvent;
