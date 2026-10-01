using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

public sealed class GetEvidenceRequestHandler(IAggregateReader reader)
    : IRequestHandler<GetEvidenceRequest, EvidenceRequestView>
{
    public async ValueTask<Result<EvidenceRequestView>> HandleAsync(IRequestContext<GetEvidenceRequest> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var ledger = await reader.HydrateAsync(new EvidenceRequestLedger(request.TenantId, request.ProgramId), ct)
            .ConfigureAwait(false);
        return ledger.Find(request.EvidenceRequestId) is { } found
            ? Result<EvidenceRequestView>.Success(found)
            : Result<EvidenceRequestView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The evidence request was not found."));
    }
}
