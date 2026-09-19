using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public interface ITenantActivity
{
    ValueTask<bool> IsActiveAsync(Uuid tenantId, CancellationToken ct = default);
}
