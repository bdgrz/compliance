using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

/// <summary>
///     Approves the exact reviewed revision. A successor must acknowledge the complete, unchanged
///     impact preview. HTTP-only personal approval.
/// </summary>
public sealed class ApprovePolicyHandler(IAggregateExecutor executor, IAggregateReader reader,
    PolicyImpactService impact, TimeProvider clock)
    : IRequestHandler<ApprovePolicy, PolicyVersionView>
{
    public async ValueTask<Result<PolicyVersionView>> HandleAsync(
        IRequestContext<ApprovePolicy> context, CancellationToken ct)
    {
        var request = context.Request;
        var current = await PolicySource.ReadAsync(reader, request.TenantId, request.ProgramId,
            request.PolicyId, ct).ConfigureAwait(false);
        if (!current.IsSuccess)
            return Result<PolicyVersionView>.Failure(current.Error);
        if (current.Value.DraftPredecessorVersion is not null)
        {
            var preview = await impact.PreviewAsync(request.TenantId, request.ProgramId,
                request.PolicyId, request.ExpectedRevision, ct).ConfigureAwait(false);
            if (!preview.IsSuccess)
                return Result<PolicyVersionView>.Failure(preview.Error);
            if (!StringComparer.Ordinal.Equals(preview.Value.Digest, request.ImpactDigest))
                return Result<PolicyVersionView>.Failure(new RequestError(
                    RequestErrorKind.Conflict, "The impact preview changed. Reload it before approving."));
        }
        var actor = PolicyActor.From(context, request.TenantId);
        var waiver = await PolicySource.WaiverAsync(reader, request.TenantId,
            request.SeparationOfDutiesWaiverId, ct).ConfigureAwait(false);
        return await executor.ExecuteAsync(new Policy(request.TenantId, request.PolicyId),
            policy =>
            {
                var failure = policy.Approve(request.ProgramId, request.ExpectedRevision,
                    context.RequestId, request.AcceptedReviewDecisionId, request.EffectiveFrom,
                    request.Major, request.Rationale, request.ImpactDigest, actor.Reference,
                    actor.MemberId, clock.GetUtcNow(), waiver);
                return CommandFailureRequestAdapter.ToOutcome(failure, policy.CurrentVersion!);
            }, context, ct).ConfigureAwait(false);
    }
}
