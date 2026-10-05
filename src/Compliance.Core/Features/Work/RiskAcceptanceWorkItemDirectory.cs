using Bdgrz.Compliance.Features.Risks;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Builds risk acceptance work from the existing risk evaluation projection.</summary>
sealed class RiskAcceptanceWorkItemDirectory(IAggregateReader reader,
    IRiskDraftDirectoryReader risks, RiskDraftListReadConsistency? riskConsistency,
    IRiskEvaluationDirectoryReader evaluations) : IAccountableWorkItemDirectoryReader
{
    public string ProjectorName => FitzRiskEvaluationDirectory.ProjectorName;

    public IReadOnlyCollection<string> ProjectedKinds => RiskAcceptanceWork.ProjectedKinds;

    public EventStreamPattern SourcePattern(Uuid tenantId) =>
        EventStreamPattern.ForPattern(tenantId.ToString(), "risk-evaluations");

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) => evaluations.LoadCheckpointAsync(tenantId, ct);

    public ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId,
        Uuid programId, CancellationToken ct = default) => LoadProgramAsync(tenantId, programId,
        DateTimeOffset.UtcNow, DateOnly.MaxValue, null, ct);

    public ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId,
        Uuid programId, DateOnly today, DateOnly horizon, Uuid? workItemId,
        CancellationToken ct = default) => LoadProgramAsync(tenantId, programId,
        new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), horizon,
        workItemId, ct);

    public ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId,
        Uuid programId, DateTimeOffset now, DateOnly horizon, Uuid? workItemId,
        CancellationToken ct = default)
    {
        _ = horizon;
        return RiskAcceptanceWork.LoadProjectedAsync(reader, risks, riskConsistency, evaluations,
            tenantId, programId, now, workItemId, ct);
    }
}
