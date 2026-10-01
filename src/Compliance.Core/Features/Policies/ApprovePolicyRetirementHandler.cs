using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

/// <summary>Records the acting member's own retirement approval; HTTP-only.</summary>
public sealed class ApprovePolicyRetirementHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<ApprovePolicyRetirement, PolicyDecisionView>
{
    public async ValueTask<Result<PolicyDecisionView>> HandleAsync(
        IRequestContext<ApprovePolicyRetirement> context, CancellationToken ct)
    {
        var request = context.Request;
        var actor = PolicyActor.From(context, request.TenantId);
        var waiver = await PolicySource.WaiverAsync(reader, request.TenantId,
            request.SeparationOfDutiesWaiverId, ct).ConfigureAwait(false);
        return await executor.ExecuteAsync(new Policy(request.TenantId, request.PolicyId),
            policy =>
            {
                var failure = policy.ApproveRetirement(request.ProgramId,
                    request.ExpectedRevision, context.RequestId, request.AcceptedReviewDecisionId,
                    request.Rationale, actor.Reference, actor.MemberId, clock.GetUtcNow(),
                    waiver);
                return CommandFailureRequestAdapter.ToOutcome(failure,
                    policy.FindDecision(context.RequestId)!);
            }, context, ct).ConfigureAwait(false);
    }
}
