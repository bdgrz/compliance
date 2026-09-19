using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant-slug.resolve-mine", 1)]
public sealed record ResolveMyTenantSlug(string Slug) : IRequest<TenantSlugResolution>, ICallable;
