using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant-slug-surrender.request", 1)]
public sealed record RequestTenantSlugSurrender(Uuid TenantId, string Slug) : IRequest, ICallable;
