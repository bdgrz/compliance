using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

public sealed class ListAccessExpectationsHandler(IAggregateReader reader)
    : IRequestHandler<ListAccessExpectations, AccessExpectationsView>
{
    public async ValueTask<Result<AccessExpectationsView>> HandleAsync(
        IRequestContext<ListAccessExpectations> context, CancellationToken ct)
    {
        var request = context.Request;
        var ledger = await reader.HydrateAsync(new AccessReviewSystemLedger(request.TenantId,
            request.SystemInstanceId), ct).ConfigureAwait(false);
        return Result<AccessExpectationsView>.Success(ledger.ToView());
    }
}
