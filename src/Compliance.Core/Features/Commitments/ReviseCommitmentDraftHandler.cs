using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

public sealed class ReviseCommitmentDraftHandler(IAggregateExecutor executor,
    TimeProvider clock) : IRequestHandler<ReviseCommitmentDraft>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviseCommitmentDraft> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        return executor.ExecuteAsync(new CommitmentDraft(request.TenantId, request.DraftId),
            draft => AggregateOutcome.CommitOnSuccess(draft.Revise(request.ProgramId,
                request.ExpectedRevision, request.Statement, request.Context,
                request.SourceReference, RbacIds.Member(request.TenantId, userId),
                UserIdentityClaims.BdgrzDisplay(context.Actor, userId), clock.GetUtcNow())),
            context, ct);
    }
}
