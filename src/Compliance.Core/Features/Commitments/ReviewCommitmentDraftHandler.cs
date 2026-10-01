using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

public sealed class ReviewCommitmentDraftHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<ReviewCommitmentDraft>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ReviewCommitmentDraft> context,
        CancellationToken ct)
    {
        var request = context.Request;
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
                request.InterpretationNote, request.Rationale,
                RbacIds.Member(request.TenantId, userId),
                UserIdentityClaims.BdgrzDisplay(context.Actor, userId), clock.GetUtcNow(),
                waiver, request.SourceVerifiedReference, request.SourceEvidence)),
            context, ct).ConfigureAwait(false);
    }
}
