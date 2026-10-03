using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

public interface IReadinessReadModel
{
    ValueTask<Result<ReadinessAssessmentView>> GetAssessmentAsync(
        GetReadinessAssessment request, CancellationToken ct);

    ValueTask<Result<Page<ReadinessAssessmentSummaryView>>> ListAssessmentsAsync(
        ListReadinessAssessments request, CancellationToken ct);

    ValueTask<Result<Page<ReadinessGapView>>> ListGapsAsync(ListReadinessGaps request,
        CancellationToken ct);

    ValueTask<Result<Page<ReadinessAnnotationView>>> ListAnnotationsAsync(
        ListReadinessAnnotations request, CancellationToken ct);

    ValueTask<Result<Page<TypeIEntryDecisionView>>> ListTypeIEntryDecisionsAsync(
        ListTypeIEntryDecisions request, CancellationToken ct);
}
