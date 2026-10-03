using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>Lists an assessment's gaps joined with the current gap plan.</summary>
public sealed class ListReadinessGapsHandler(IReadinessReadModel readModel)
    : IRequestHandler<ListReadinessGaps, Page<ReadinessGapView>>
{
    public ValueTask<Result<Page<ReadinessGapView>>> HandleAsync(
        IRequestContext<ListReadinessGaps> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.PlanState is not (null or "planned" or "unplanned"))
            return ValueTask.FromResult(Result<Page<ReadinessGapView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The plan state filter must be planned or unplanned.")));
        return readModel.ListGapsAsync(request, ct);
    }
}
