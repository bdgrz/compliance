using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
/// Finds one historical declaration without retaining every instance in the application
/// aggregate. A projected legacy row is authoritative; the parent stream is read only when the
/// V2 projection may still be missing that declaration.
/// </summary>
public sealed class LegacySystemInstanceSource(IApplicationDirectoryReader directory,
    IDomainEventReader events)
{
    public async ValueTask<bool> ExistsAsync(Uuid tenantId, Uuid applicationId,
        Uuid instanceId, CancellationToken ct)
    {
        var projected = await directory.GetInstanceAsync(tenantId, instanceId, ct)
            .ConfigureAwait(false);
        if (projected is { LegacyApplicationRevision: not null } &&
            projected.TenantId == tenantId && projected.SystemInstanceId == instanceId)
            return projected.ApplicationId == applicationId;
        var pending = await ApplicationDirectoryBacklog.FindAsync(directory, events, tenantId,
            ev => ev is SystemInstanceDeclared declared && declared.SystemInstanceId == instanceId,
            ct).ConfigureAwait(false);
        if (pending.Match is SystemInstanceDeclared match)
            return match.ApplicationId == applicationId;
        if (pending.Clear)
            return false;
        var address = new EventStreamAddress(tenantId.ToString(), "applications",
            applicationId.ToString());
        await foreach (var record in events.ReadAsync(address, 0, ct).ConfigureAwait(false))
        {
            if (record.Event is SystemInstanceDeclared ev &&
                ev.TenantId == tenantId && ev.ApplicationId == applicationId &&
                ev.SystemInstanceId == instanceId)
                return true;
        }
        return false;
    }
}
