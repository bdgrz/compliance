using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public interface IApplicationInventoryActivity
{
    ValueTask<bool> IsDeclaredAsync(Uuid tenantId, Uuid applicationId,
        CancellationToken ct = default);

    ValueTask<SystemInstanceReferenceState> GetInstanceStateAsync(Uuid tenantId,
        Uuid instanceId, CancellationToken ct = default);
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

    public async ValueTask<SystemInstanceReferenceState> GetInstanceStateAsync(Uuid tenantId,
        Uuid instanceId, CancellationToken ct = default)
    {
        var source = await reader.HydrateAsync(new DeclaredSystemInstance(tenantId,
            instanceId), ct).ConfigureAwait(false);
        var instance = await directory.GetInstanceAsync(tenantId, instanceId, ct)
            .ConfigureAwait(false);
        if (instance is not null && (instance.TenantId != tenantId ||
                                     instance.SystemInstanceId != instanceId))
            return SystemInstanceReferenceState.Missing;
        if (source.IsCreated)
        {
            // Reference commands wait for the tenant-scoped instance projection, so a
            // just-committed source is a recoverable conflict until visible.
            if (instance is null || instance.Revision < source.Revision)
                return SystemInstanceReferenceState.Pending;
            return instance.ApplicationId == source.ApplicationId
                ? SystemInstanceReferenceState.Declared
                : SystemInstanceReferenceState.Missing;
        }
        if (instance is null)
            return (await legacy.FindPendingAsync(tenantId, instanceId, ct)
                    .ConfigureAwait(false)).Clear
                ? SystemInstanceReferenceState.Missing
                : SystemInstanceReferenceState.Pending;
        // Historical declarations live in application streams; a projected legacy row is
        // authoritative, so no stream is rebuilt for the common case.
        return await legacy.ExistsAsync(tenantId, instance.ApplicationId, instanceId, ct)
            .ConfigureAwait(false)
            ? SystemInstanceReferenceState.Declared
            : SystemInstanceReferenceState.Missing;
    }
}
