using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class GetTenantProvisioningStatusHandler(IAggregateReader reader)
    : IRequestHandler<GetTenantProvisioningStatus, TenantProvisioningStatusView>
{
    public async ValueTask<Result<TenantProvisioningStatusView>> HandleAsync(
        IRequestContext<GetTenantProvisioningStatus> context, CancellationToken ct)
    {
        var tenant = await reader.HydrateAsync(new Tenant(context.Request.TenantId), ct).ConfigureAwait(false);
        return Result<TenantProvisioningStatusView>.Success(tenant.GetProvisioningStatus());
    }
}
