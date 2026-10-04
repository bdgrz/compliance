using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant.offboard", 1)]
public sealed record OffboardTenant(Uuid TenantId, string Reason) : IRequest, ICallable, IPlatformOperatorRequest;
