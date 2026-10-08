using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class GetClientIndependenceHistoryHandler(IAggregateReader reader)
    : IRequestHandler<GetClientIndependenceHistory, IndependenceHistoryView>
{
    public async ValueTask<Result<IndependenceHistoryView>> HandleAsync(
        IRequestContext<GetClientIndependenceHistory> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty)
            return Result<IndependenceHistoryView>.Failure(new RequestError(RequestErrorKind.Validation,
                "Independence history requires its owning client tenant."));
        var ledger = await reader.HydrateAsync(new IndependenceLedger(request.TenantId), ct).ConfigureAwait(false);
        return Result<IndependenceHistoryView>.Success(ledger.History());
    }
}
