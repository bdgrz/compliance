using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant.suspend", 2)]
public sealed record SuspendTenant(Uuid TenantId, string Reason) : IRequest, ICallable, IPlatformOperatorRequest;
