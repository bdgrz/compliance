using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant.suspend", 1)]
public sealed record SuspendTenant(Uuid TenantId) : IRequest, ICallable, IPlatformOperatorRequest;
