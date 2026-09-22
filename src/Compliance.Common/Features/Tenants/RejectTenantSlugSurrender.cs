using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant-slug-surrender.reject", 1)]
public sealed record RejectTenantSlugSurrender(Uuid TenantId, string Slug) : IRequest,
    ITenantLifecycleReactionRequest;
