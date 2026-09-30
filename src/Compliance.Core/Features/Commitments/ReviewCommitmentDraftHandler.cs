using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

public sealed class ReviewCommitmentDraftHandler(IAggregateExecutor executor,
    IAggregateReader reader, CommitmentImpactService impact, TimeProvider clock)
    : IRequestHandler<ReviewCommitmentDraft>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ReviewCommitmentDraft> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (StringComparer.Ordinal.Equals(request.Outcome, "accept"))
        {
            var preview = await impact.PreviewAsync(new PreviewCommitmentImpact(request.TenantId,
                request.ProgramId, request.DraftId, request.ExpectedRevision), ct)
                .ConfigureAwait(false);
            if (!preview.IsSuccess)
                return Result.Failure(preview.Error);
            if (!preview.Value.Complete)
                return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The impact preview is incomplete. Acceptance cannot acknowledge it."));
            if (!StringComparer.Ordinal.Equals(request.ImpactDigest, preview.Value.Digest))
                return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The impact preview changed. Reload it before accepting."));
        }
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        SeparationOfDutiesWaiver? waiver = null;
        if (request.SeparationOfDutiesWaiverId is { } waiverId)
            waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId,
                waiverId), ct).ConfigureAwait(false);
        return await executor.ExecuteAsync(new CommitmentDraft(request.TenantId, request.DraftId),
            draft => CommandFailureRequestAdapter.ToOutcome(draft.Review(request.ProgramId,
                request.ExpectedRevision, context.RequestId, request.Outcome,
                request.OwnerReference, request.Applicability, request.Interpretation,
                request.InterpretationNote, request.Rationale, request.EffectiveFrom,
                request.ImpactDigest, RbacIds.Member(request.TenantId, userId),
                UserIdentityClaims.BdgrzDisplay(context.Actor, userId), clock.GetUtcNow(),
                waiver)),
            context, ct).ConfigureAwait(false);
    }
}
