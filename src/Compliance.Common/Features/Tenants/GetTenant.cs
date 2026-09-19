using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant.get", 1)]
public sealed record GetTenant(Uuid TenantId) : IRequest<TenantView>, ICallable;
