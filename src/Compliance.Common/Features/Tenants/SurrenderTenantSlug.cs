using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant-slug.surrender", 1)]
public sealed record SurrenderTenantSlug(Uuid TenantId, string Slug) : IRequest, ITenantLifecycleReactionRequest;
