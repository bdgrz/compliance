using Bdgrz.Compliance.Features.Controls;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>Lists a program's assessments, newest first.</summary>
public sealed class ListReadinessAssessmentsHandler(IAggregateReader reader)
    : IRequestHandler<ListReadinessAssessments, Page<ReadinessAssessmentSummaryView>>
{
    public async ValueTask<Result<Page<ReadinessAssessmentSummaryView>>> HandleAsync(
        IRequestContext<ListReadinessAssessments> context, CancellationToken ct)
    {
        var request = context.Request;
        var ledger = await reader.HydrateAsync(new ReadinessLedger(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        return ControlActivationSource.Paginate(ledger.Summaries(), request.Limit,
            request.Cursor, "readiness assessments");
    }
}
