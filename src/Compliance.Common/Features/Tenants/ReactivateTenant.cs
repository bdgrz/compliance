using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant.reactivate", 2)]
public sealed record ReactivateTenant(Uuid TenantId, string Reason) : IRequest, ICallable, IPlatformOperatorRequest;
