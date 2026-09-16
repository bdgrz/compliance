using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed record TenantRegistration(Uuid TenantId, string Slug);
