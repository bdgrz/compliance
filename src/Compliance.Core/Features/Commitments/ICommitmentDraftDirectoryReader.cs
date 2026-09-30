using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

public interface ICommitmentDraftDirectoryReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default);
    ValueTask<CommitmentDraftView?> GetAsync(Uuid tenantId, Uuid draftId,
        CancellationToken ct = default);
    ValueTask<Page<CommitmentDraftView>> ListProgramAsync(Uuid tenantId, Uuid programId,
        int limit, string? cursor, CancellationToken ct = default);
    ValueTask<CommitmentDraftRevisionView?> GetRevisionAsync(Uuid tenantId, Uuid draftId,
        long revision, CancellationToken ct = default);
    ValueTask<CommitmentVersionView?> GetVersionAsync(Uuid tenantId, Uuid draftId,
        long version, CancellationToken ct = default);
    ValueTask<Page<CommitmentVersionView>> ListVersionsAsync(Uuid tenantId, Uuid draftId,
        int limit, string? cursor, CancellationToken ct = default);
    ValueTask<Page<CommitmentDecisionView>> ListDecisionsAsync(Uuid tenantId, Uuid draftId,
        int limit, string? cursor, CancellationToken ct = default);
    ValueTask<CommitmentDecisionView?> GetDecisionAsync(Uuid tenantId, Uuid decisionId,
        CancellationToken ct = default);
}
