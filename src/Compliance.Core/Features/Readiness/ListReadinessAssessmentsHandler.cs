using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>Lists a program's assessments, newest first.</summary>
public sealed class ListReadinessAssessmentsHandler(IReadinessReadModel readModel)
    : IRequestHandler<ListReadinessAssessments, Page<ReadinessAssessmentSummaryView>>
{
    public ValueTask<Result<Page<ReadinessAssessmentSummaryView>>> HandleAsync(
        IRequestContext<ListReadinessAssessments> context, CancellationToken ct) =>
        readModel.ListAssessmentsAsync(context.Request, ct);
}
