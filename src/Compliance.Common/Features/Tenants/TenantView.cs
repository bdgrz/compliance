using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed record TenantView(Uuid TenantId, string Name, string Slug);
