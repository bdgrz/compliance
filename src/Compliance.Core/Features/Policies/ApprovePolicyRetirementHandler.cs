using Bdgrz.Compliance.Features.AccessControl;
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
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Result<PolicyDecisionView>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Policy sign-off requires personal HTTP submission."));
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
