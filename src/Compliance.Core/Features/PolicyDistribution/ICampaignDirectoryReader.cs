using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

public interface ICampaignDirectoryReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default);
    ValueTask<Page<CampaignSummaryView>> ListProgramAsync(Uuid tenantId, Uuid programId,
        int limit, string? cursor, CancellationToken ct = default);

    /// <summary>The program's campaigns bound to one exact subject version.</summary>
    ValueTask<IReadOnlyList<Uuid>> ListForSubjectAsync(Uuid tenantId, Uuid programId,
        Uuid subjectId, long version, CancellationToken ct = default);
}
