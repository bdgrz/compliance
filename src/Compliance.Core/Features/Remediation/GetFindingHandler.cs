using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

public sealed class GetFindingHandler(IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<GetFinding, FindingView>
{
    public async ValueTask<Result<FindingView>> HandleAsync(IRequestContext<GetFinding> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var ledger = await reader.HydrateAsync(new RemediationLedger(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        return ledger.Read(request.FindingId, clock.GetUtcNow()) is { } view
            ? Result<FindingView>.Success(view)
            : Result<FindingView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The finding was not found."));
    }
}
