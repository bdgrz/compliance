using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

public interface IPolicyDirectoryReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default);
    ValueTask<Page<PolicySummaryView>> ListProgramAsync(Uuid tenantId, Uuid programId,
        int limit, string? cursor, CancellationToken ct = default);
}
