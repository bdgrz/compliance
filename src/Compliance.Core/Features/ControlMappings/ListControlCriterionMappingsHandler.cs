using Bdgrz.Compliance.Features.Controls;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

public sealed class ListControlCriterionMappingsHandler(IAggregateReader reader)
    : IRequestHandler<ListControlCriterionMappings, Page<ControlCriterionMappingView>>
{
    public async ValueTask<Result<Page<ControlCriterionMappingView>>> HandleAsync(
        IRequestContext<ListControlCriterionMappings> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Status is not (null or "pending" or "active" or "rejected" or "retired"))
            return Result<Page<ControlCriterionMappingView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The mapping status filter must be pending, active, rejected, or retired."));
        var ledger = await reader.HydrateAsync(new ControlCriterionMappingLedger(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        var items = ledger.ReadAll().Where(mapping =>
                (request.ControlId is null || mapping.ControlId == request.ControlId) &&
                (request.EditionId is null || mapping.EditionId == request.EditionId) &&
                (request.Status is null || mapping.Status == request.Status))
            .ToArray();
        return ControlActivationSource.Paginate(items, request.Limit, request.Cursor,
            "control criterion mapping");
    }
}
