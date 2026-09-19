using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

sealed class ActiveTenant : ITenantActivity
{
    public ValueTask<bool> IsActiveAsync(Uuid tenantId, CancellationToken ct = default) =>
        ValueTask.FromResult(true);
}
