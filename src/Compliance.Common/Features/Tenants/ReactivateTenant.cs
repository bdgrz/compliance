using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant.reactivate", 1)]
public sealed record ReactivateTenant(Uuid TenantId) : IRequest, ICallable, IPlatformOperatorRequest;
