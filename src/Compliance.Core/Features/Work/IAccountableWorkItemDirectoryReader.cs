using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public interface IAccountableWorkItemDirectoryReader
{
    string ProjectorName { get; }

    IReadOnlyCollection<string> ProjectedKinds { get; }

    EventStreamPattern SourcePattern(Uuid tenantId);

    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default);

    ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId,
        Uuid programId, CancellationToken ct = default);

    ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId,
        Uuid programId, DateOnly today, DateOnly horizon, Uuid? workItemId,
        CancellationToken ct = default) => LoadProgramAsync(tenantId, programId, ct);
}
