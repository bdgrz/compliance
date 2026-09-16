using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant-slug.confirm", 1)]
public sealed record ConfirmTenantSlug(Uuid TenantId, string Slug) : IRequest;
