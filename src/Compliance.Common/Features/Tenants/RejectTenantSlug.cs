using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant-slug.reject", 1)]
public sealed record RejectTenantSlug(Uuid TenantId, string Slug) : IRequest;
