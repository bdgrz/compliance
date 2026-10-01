using Bdgrz.Compliance.Features.Controls;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>Lists an assessment's gaps joined with the current gap plan.</summary>
public sealed class ListReadinessGapsHandler(IAggregateReader reader)
    : IRequestHandler<ListReadinessGaps, Page<ReadinessGapView>>
{
    public async ValueTask<Result<Page<ReadinessGapView>>> HandleAsync(
        IRequestContext<ListReadinessGaps> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.PlanState is not (null or "planned" or "unplanned"))
            return Result<Page<ReadinessGapView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The plan state filter must be planned or unplanned."));
        var ledger = await reader.HydrateAsync(new ReadinessLedger(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        if (ledger.ReadGaps(request.AssessmentId) is not { } gaps)
            return Result<Page<ReadinessGapView>>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The readiness assessment was not found."));
        var items = gaps.Where(gap =>
            (request.PlanState is null ||
             (gap.Plan is not null) == (request.PlanState == "planned")) &&
            (request.OwnerMemberId is not { } owner || gap.Plan?.OwnerMemberId == owner) &&
            (request.Kind is null || gap.Kind == request.Kind) &&
            (request.RuleId is null || gap.RuleId == request.RuleId) &&
            (request.Subject is null || gap.Subject == request.Subject)).ToArray();
        return ControlActivationSource.Paginate(items, request.Limit, request.Cursor,
            "readiness gaps");
    }
}
