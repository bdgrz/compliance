using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

public sealed class ConfirmPolicyReviewHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<ConfirmPolicyReview, PolicyDecisionView>
{
    public ValueTask<Result<PolicyDecisionView>> HandleAsync(
        IRequestContext<ConfirmPolicyReview> context, CancellationToken ct)
    {
        var request = context.Request;
        var actor = PolicyActor.From(context, request.TenantId);
        return executor.ExecuteAsync(new Policy(request.TenantId, request.PolicyId),
            policy =>
            {
                var failure = policy.ConfirmPeriodicReview(request.ProgramId,
                    request.ExpectedVersion, context.RequestId, request.Rationale,
                    actor.Reference, actor.MemberId, clock.GetUtcNow());
                return CommandFailureRequestAdapter.ToOutcome(failure,
                    policy.FindDecision(context.RequestId)!);
            }, context, ct);
    }
}
