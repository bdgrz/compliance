using Bdgrz.Compliance.Features.Controls;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>Lists a program's applicability decisions from the caught-up projection.</summary>
public sealed class ListCriterionApplicabilityHandler(CriteriaCoverageReadConsistency coverage)
    : IRequestHandler<ListCriterionApplicability, Page<CriterionApplicabilityView>>
{
    public async ValueTask<Result<Page<CriterionApplicabilityView>>> HandleAsync(
        IRequestContext<ListCriterionApplicability> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Status is not (null or "pending" or "not_applicable" or "rejected" or
            "withdrawn"))
            return Result<Page<CriterionApplicabilityView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The applicability status filter must be pending, not_applicable, rejected, or withdrawn."));
        var projected = await coverage.ReadDecisionsAsync(request.TenantId, request.ProgramId,
            ct).ConfigureAwait(false);
        if (!projected.IsSuccess)
            return Result<Page<CriterionApplicabilityView>>.Failure(projected.Error);
        var items = projected.Value.Where(decision =>
                (request.EditionId is null || decision.EditionId == request.EditionId) &&
                (request.Status is null || decision.Status == request.Status))
            .ToArray();
        return ControlActivationSource.Paginate(items, request.Limit, request.Cursor,
            "criterion applicability");
    }
}
