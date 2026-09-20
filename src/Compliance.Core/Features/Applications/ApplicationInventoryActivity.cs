using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public interface IApplicationInventoryActivity
{
    ValueTask<bool> IsDeclaredAsync(Uuid tenantId, Uuid applicationId,
        CancellationToken ct = default);

    ValueTask<bool> IsInstanceDeclaredAsync(Uuid tenantId, Uuid instanceId,
        CancellationToken ct = default);
}

sealed class EventSourcedApplicationInventoryActivity(IAggregateReader reader,
    IApplicationDirectoryReader directory) : IApplicationInventoryActivity
{
    public async ValueTask<bool> IsDeclaredAsync(Uuid tenantId, Uuid applicationId,
        CancellationToken ct = default)
    {
        var application = await reader.HydrateAsync(
            new DeclaredApplication(tenantId, applicationId), ct).ConfigureAwait(false);
        return application.IsCreated;
    }

    public async ValueTask<bool> IsInstanceDeclaredAsync(Uuid tenantId, Uuid instanceId,
        CancellationToken ct = default)
    {
        // The tenant-scoped directory locates the aggregate that owns this instance.
        // A lagging projection produces a recoverable conflict at the boundary command.
        var instance = await directory.GetInstanceAsync(tenantId, instanceId, ct)
            .ConfigureAwait(false);
        if (instance is null || instance.TenantId != tenantId ||
            instance.SystemInstanceId != instanceId)
            return false;
        var application = await reader.HydrateAsync(
            new DeclaredApplication(tenantId, instance.ApplicationId), ct).ConfigureAwait(false);
        return application.HasInstance(instanceId);
    }
}
