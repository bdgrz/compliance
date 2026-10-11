using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant.provisioning_status.get", 1)]
public sealed record GetTenantProvisioningStatus(Uuid TenantId) : IRequest<TenantProvisioningStatusView>, ICallable;
