using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

public sealed class ApproveCommitmentDraftHandler(IAggregateExecutor executor,
    IAggregateReader reader, CommitmentImpactService impact, TimeProvider clock)
    : IRequestHandler<ApproveCommitmentDraft>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ApproveCommitmentDraft> context,
        CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Commitment sign-off requires personal HTTP submission."));
        var request = context.Request;
        var preview = await impact.PreviewAsync(new PreviewCommitmentImpact(request.TenantId,
            request.ProgramId, request.DraftId, request.ExpectedRevision), ct)
            .ConfigureAwait(false);
        if (!preview.IsSuccess)
            return Result.Failure(preview.Error);
        if (!preview.Value.Complete)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The impact preview is incomplete. Approval cannot acknowledge it."));
        if (!StringComparer.Ordinal.Equals(request.ImpactDigest, preview.Value.Digest))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The impact preview changed. Reload it before approval."));
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        SeparationOfDutiesWaiver? waiver = null;
        if (request.SeparationOfDutiesWaiverId is { } waiverId)
            waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId,
                waiverId), ct).ConfigureAwait(false);
        return await executor.ExecuteAsync(new CommitmentDraft(request.TenantId, request.DraftId),
            draft => CommandFailureRequestAdapter.ToOutcome(draft.Approve(request.ProgramId,
                request.ExpectedRevision, context.RequestId, request.AcceptedReviewDecisionId,
                request.EffectiveFrom, request.Rationale, request.ImpactDigest,
                RbacIds.Member(request.TenantId, userId),
                UserIdentityClaims.BdgrzDisplay(context.Actor, userId), clock.GetUtcNow(),
                waiver)),
            context, ct).ConfigureAwait(false);
    }
}
