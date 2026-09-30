using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Resolves a scope subject from its instance stream, then its legacy application stream.</summary>
public static class ScopedSystemInstanceSource
{
    public static async ValueTask<ScopedSystemInstance?> FindAsync(IAggregateReader reader,
        IDomainEventReader events, Uuid tenantId, Uuid applicationId, Uuid instanceId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var instance = await reader.HydrateAsync(new DeclaredSystemInstance(tenantId,
            instanceId), ct).ConfigureAwait(false);
        if (instance.IsCreated)
            return instance.ApplicationId == applicationId
                ? ScopedSystemInstance.From(instance)
                : null;
        var legacy = await LegacySystemInstanceSource.ReadDeclarationAsync(events, tenantId,
            applicationId, instanceId, ct).ConfigureAwait(false);
        return legacy is null ? null : ScopedSystemInstance.FromLegacy(legacy);
    }
}
