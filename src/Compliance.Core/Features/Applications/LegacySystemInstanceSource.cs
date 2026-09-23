using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Finds one historical declaration without retaining every instance in the application aggregate.</summary>
public sealed class LegacySystemInstanceSource(IDomainEventReader events)
{
    public async ValueTask<SystemInstanceDeclared?> FindAsync(Uuid tenantId,
        Uuid applicationId, Uuid instanceId, CancellationToken ct)
    {
        var address = new EventStreamAddress(tenantId.ToString(), "applications",
            applicationId.ToString());
        await foreach (var record in events.ReadAsync(address, 0, ct).ConfigureAwait(false))
        {
            if (record.Event is SystemInstanceDeclared ev &&
                ev.TenantId == tenantId && ev.ApplicationId == applicationId &&
                ev.SystemInstanceId == instanceId)
                return ev;
        }
        return null;
    }
}
