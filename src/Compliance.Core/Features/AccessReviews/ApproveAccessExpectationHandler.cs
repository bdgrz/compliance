using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Records the acting member's personal expectation approval; HTTP-only, never an MCP tool.</summary>
public sealed class ApproveAccessExpectationHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<ApproveAccessExpectation, AccessExpectationView>
{
    public async ValueTask<Result<AccessExpectationView>> HandleAsync(
        IRequestContext<ApproveAccessExpectation> context, CancellationToken ct)
    {
        var request = context.Request;
        SeparationOfDutiesWaiver? waiver = null;
        if (request.SeparationOfDutiesWaiverId is { } waiverId)
            waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId,
                waiverId), ct).ConfigureAwait(false);
        var actor = AccessReviewActor.From(context);
        return await executor.ExecuteAsync(new AccessReviewSystemLedger(request.TenantId,
                request.SystemInstanceId),
            ledger => AccessReviewOutcome.From(ledger.Approve(request.ExpectationId,
                request.ExpectedLedgerRevision, actor.MemberId, actor.Reference, clock.GetUtcNow(),
                waiver)), context, ct).ConfigureAwait(false);
    }
}
