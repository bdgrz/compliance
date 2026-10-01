using Bdgrz.Compliance.Features.Controls;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

public sealed class ListEvidenceRequestsHandler(IAggregateReader reader)
    : IRequestHandler<ListEvidenceRequests, Page<EvidenceRequestView>>
{
    public async ValueTask<Result<Page<EvidenceRequestView>>> HandleAsync(
        IRequestContext<ListEvidenceRequests> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Status is not (null or EvidenceRequestLedger.Open or EvidenceRequestLedger.Fulfilled or
            EvidenceRequestLedger.Cancelled))
            return Result<Page<EvidenceRequestView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The status filter must be open, fulfilled, or cancelled."));
        var ledger = await reader.HydrateAsync(new EvidenceRequestLedger(request.TenantId, request.ProgramId), ct)
            .ConfigureAwait(false);
        var items = ledger.ReadAll().Where(item => request.Status is null || item.Status == request.Status)
            .ToArray();
        return ControlActivationSource.Paginate(items, request.Limit, request.Cursor, "evidence requests");
    }
}
