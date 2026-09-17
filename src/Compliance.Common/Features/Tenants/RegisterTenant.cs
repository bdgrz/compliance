using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant.register", 1)]
public sealed record RegisterTenant(string Name, string Slug) : IRequest<TenantRegistration>, ICallable;
