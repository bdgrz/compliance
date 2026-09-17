using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant-slug.register", 1)]
public sealed record RegisterTenantSlug(Uuid TenantId, string Slug) : IRequest;
