using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Records a Compliance Lead's effective-dated access-review scope decision.</summary>
public sealed class DecideAccessReviewScopeHandler(IAggregateExecutor executor,
    IAggregateReader reader, IDomainEventReader events, TimeProvider clock)
    : IRequestHandler<DecideAccessReviewScope, AccessReviewScopeDecisionView>
{
    public async ValueTask<Result<AccessReviewScopeDecisionView>> HandleAsync(
        IRequestContext<DecideAccessReviewScope> context, CancellationToken ct)
    {
        var (memberId, display) = ApplicationActor.From(context);
        var request = context.Request;
        if (request.ExpectedSystemInstanceRevision < 1 || request.ExpectedDecisionCount < 0)
            return Result<AccessReviewScopeDecisionView>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The expected instance revision must be positive and the decision count non-negative."));
        var instance = await ScopedSystemInstanceSource.FindAsync(reader, events,
            request.TenantId, request.ApplicationId, request.SystemInstanceId, ct)
            .ConfigureAwait(false);
        if (instance is null)
            return Result<AccessReviewScopeDecisionView>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The system instance was not found."));
        if (instance.Revision != request.ExpectedSystemInstanceRevision)
            return Result<AccessReviewScopeDecisionView>.Failure(new RequestError(
                RequestErrorKind.Conflict,
                $"The system instance is at revision {instance.Revision}; the decision names revision {request.ExpectedSystemInstanceRevision}."));
        SeparationOfDutiesWaiver? waiver = null;
        if (request.SeparationOfDutiesWaiverId is { } waiverId)
            waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId,
                waiverId), ct).ConfigureAwait(false);
        var now = clock.GetUtcNow();
        return await executor.ExecuteAsync(new SystemInstanceAccessReviewScope(request.TenantId,
                request.SystemInstanceId),
            scope =>
            {
                var result = scope.Decide(instance, request.ExpectedDecisionCount,
                    context.RequestId, request.Decision, request.Reason, request.EffectiveFrom,
                    request.ReviewBy, memberId, display, now, waiver);
                return result.IsSuccess
                    ? AggregateOutcome.Commit(result)
                    : AggregateOutcome.Discard(result);
            }, context, ct).ConfigureAwait(false);
    }
}
