using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed record TenantSlugResolution(Uuid TenantId, string CurrentSlug, bool Redirect);
