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
    IApplicationDirectoryReader directory, LegacySystemInstanceSource legacy)
    : IApplicationInventoryActivity
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
        var source = await reader.HydrateAsync(new DeclaredSystemInstance(tenantId,
            instanceId), ct).ConfigureAwait(false);
        // Reference commands wait for the tenant-scoped instance projection, so a
        // just-committed source is reported as a recoverable conflict until visible.
        var instance = await directory.GetInstanceAsync(tenantId, instanceId, ct)
            .ConfigureAwait(false);
        if (instance is null || instance.TenantId != tenantId ||
            instance.SystemInstanceId != instanceId)
            return false;
        if (source.IsCreated)
            return instance.ApplicationId == source.ApplicationId &&
                   instance.Revision >= source.Revision;
        // Historical declarations live in application streams. The directory
        // locates their parent without rebuilding an unbounded aggregate map.
        return await legacy.FindAsync(tenantId, instance.ApplicationId,
            instanceId, ct).ConfigureAwait(false) is not null;
    }
}
