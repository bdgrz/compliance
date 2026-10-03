using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

public interface IReadinessDirectoryReader
{
    ValueTask<ReadinessAssessmentView?> GetAssessmentAsync(Uuid tenantId, Uuid programId,
        Uuid assessmentId, CancellationToken ct = default);

    ValueTask<Result<Page<ReadinessAssessmentSummaryView>>> ListAssessmentsAsync(Uuid tenantId,
        Uuid programId, int? limit, string? cursor, CancellationToken ct = default);

    ValueTask<Result<Page<ReadinessGapView>>> ListGapsAsync(Uuid tenantId, Uuid programId,
        Uuid assessmentId, string? planState, int? limit, string? cursor, Uuid? ownerMemberId,
        string? kind, string? ruleId, string? subject, CancellationToken ct = default);

    ValueTask<Result<Page<ReadinessAnnotationView>>> ListAnnotationsAsync(Uuid tenantId,
        Uuid programId, Uuid assessmentId, int? limit, string? cursor,
        CancellationToken ct = default);

    ValueTask<Result<Page<TypeIEntryDecisionView>>> ListTypeIEntryDecisionsAsync(Uuid tenantId,
        Uuid programId, int? limit, string? cursor, CancellationToken ct = default);

    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default);
}
