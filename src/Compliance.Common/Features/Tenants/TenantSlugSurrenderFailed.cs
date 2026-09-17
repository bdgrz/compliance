using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant-slug-surrender.failed", 1)]
public sealed record TenantSlugSurrenderFailed(Uuid TenantId, string Slug) : DomainEvent;
