using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public interface IEvidenceWorkItemDirectoryReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default);

    ValueTask<long> LoadRevisionAsync(Uuid tenantId, CancellationToken ct = default);

    ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId,
        Uuid programId, CancellationToken ct = default);
}
