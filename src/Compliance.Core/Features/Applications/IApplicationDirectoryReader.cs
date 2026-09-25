using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public interface IApplicationDirectoryReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default);
    ValueTask<ApplicationView?> GetAsync(Uuid tenantId, Uuid applicationId,
        CancellationToken ct = default);
    ValueTask<Page<ApplicationView>> ListAsync(Uuid tenantId, int limit, string? cursor,
        CancellationToken ct = default);
    ValueTask<ApplicationRevisionView?> GetRevisionAsync(Uuid tenantId, Uuid applicationId,
        long revision, CancellationToken ct = default);
    ValueTask<Page<ApplicationRevisionView>?> ListRevisionsAsync(Uuid tenantId,
        Uuid applicationId, int limit, string? cursor, CancellationToken ct = default);
    ValueTask<SystemInstanceView?> GetInstanceAsync(Uuid tenantId, Uuid instanceId,
        CancellationToken ct = default);
    ValueTask<Page<SystemInstanceView>> ListInstancesAsync(Uuid tenantId, Uuid applicationId,
        int limit, string? cursor, CancellationToken ct = default);
}
