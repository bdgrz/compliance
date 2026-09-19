using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant.change-slug", 1)]
public sealed record ChangeTenantSlug(Uuid TenantId, string Slug) : IRequest, ICallable, IPlatformOperatorRequest;
