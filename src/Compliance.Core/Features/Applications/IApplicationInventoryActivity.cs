using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public interface IApplicationInventoryActivity
{
    ValueTask<bool> IsDeclaredAsync(Uuid tenantId, Uuid applicationId,
        CancellationToken ct = default);

    ValueTask<SystemInstanceReferenceState> GetInstanceStateAsync(Uuid tenantId, Uuid instanceId,
        CancellationToken ct = default);
}
