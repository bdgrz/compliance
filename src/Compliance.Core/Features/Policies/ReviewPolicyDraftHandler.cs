using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

/// <summary>Records the acting member's own review; HTTP-only, never an MCP tool.</summary>
public sealed class ReviewPolicyDraftHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<ReviewPolicyDraft, PolicyDecisionView>
{
    public async ValueTask<Result<PolicyDecisionView>> HandleAsync(
        IRequestContext<ReviewPolicyDraft> context, CancellationToken ct)
    {
        var request = context.Request;
        var actor = PolicyActor.From(context, request.TenantId);
        var waiver = await PolicySource.WaiverAsync(reader, request.TenantId,
            request.SeparationOfDutiesWaiverId, ct).ConfigureAwait(false);
        return await executor.ExecuteAsync(new Policy(request.TenantId, request.PolicyId),
            policy =>
            {
                var failure = policy.Review(request.ProgramId, request.ExpectedRevision,
                    context.RequestId, request.Outcome, request.Rationale, actor.Reference,
                    actor.MemberId, clock.GetUtcNow(), waiver);
                return CommandFailureRequestAdapter.ToOutcome(failure,
                    policy.FindDecision(context.RequestId)!);
            }, context, ct).ConfigureAwait(false);
    }
}
