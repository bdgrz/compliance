using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public interface IApplicationBoundaryReferenceDirectory
{
    ValueTask<Page<ApplicationBoundaryReferenceView>> ListAsync(Uuid tenantId,
        string subjectType, Uuid recordId, int limit, string? cursor,
        CancellationToken ct = default);

    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default);
}
