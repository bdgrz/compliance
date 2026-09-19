using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

sealed class EventSourcedTenantActivity(IAggregateReader reader) : ITenantActivity
{
    public async ValueTask<bool> IsActiveAsync(Uuid tenantId, CancellationToken ct = default)
    {
        var tenant = await reader.HydrateAsync(new Tenant(tenantId), ct).ConfigureAwait(false);
        return tenant.IsActive;
    }
}
