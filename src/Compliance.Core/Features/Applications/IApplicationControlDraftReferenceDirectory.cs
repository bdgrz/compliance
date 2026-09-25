using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public interface IApplicationControlDraftReferenceDirectory
{
    ValueTask<Page<ApplicationControlDraftReferenceView>> ListAsync(Uuid tenantId,
        string subjectType, Uuid recordId, int limit, string? cursor,
        CancellationToken ct = default);

    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default);
}
