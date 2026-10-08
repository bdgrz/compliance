using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Records the acting member's personal expectation approval; HTTP-only, never an MCP tool.</summary>
public sealed class ApproveAccessExpectationHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock, RestrictedApplicationVisibility visibility)
    : IRequestHandler<ApproveAccessExpectation, AccessExpectationView>
{
    public async ValueTask<Result<AccessExpectationView>> HandleAsync(
        IRequestContext<ApproveAccessExpectation> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return AccessReviewOutcome.Failure<AccessExpectationView>(RequestErrorKind.Forbidden,
                "Access review sign-off requires personal HTTP submission.");
        var request = context.Request;
        var actor = AccessReviewActor.From(context);
        if (!await visibility.CanReadSystemInstanceAsync(request.TenantId, actor.UserId,
                request.SystemInstanceId, ct).ConfigureAwait(false))
            return AccessReviewOutcome.Failure<AccessExpectationView>(RequestErrorKind.NotFound,
                "The system instance was not found.");
        SeparationOfDutiesWaiver? waiver = null;
        if (request.SeparationOfDutiesWaiverId is { } waiverId)
            waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId,
                waiverId), ct).ConfigureAwait(false);
        return await executor.ExecuteAsync(new AccessReviewSystemLedger(request.TenantId,
                request.SystemInstanceId),
            ledger => AccessReviewOutcome.From(ledger.Approve(request.ExpectationId,
                request.ExpectedLedgerRevision, actor.MemberId, actor.Reference, clock.GetUtcNow(),
                waiver)), context, ct).ConfigureAwait(false);
    }
}
