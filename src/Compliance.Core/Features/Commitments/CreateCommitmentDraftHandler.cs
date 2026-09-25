using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

public sealed class CreateCommitmentDraftHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<CreateCommitmentDraft, CommitmentDraftRegistration>
{
    public async ValueTask<Result<CommitmentDraftRegistration>> HandleAsync(
        IRequestContext<CreateCommitmentDraft> context, CancellationToken ct)
    {
        var request = context.Request;
        var kind = CommitmentDraft.NormalizeKind(request.Kind);
        var identifier = CommitmentDraft.NormalizeIdentifier(request.Identifier);
        var draftId = CommitmentDraft.IdFor(request.TenantId, request.ProgramId,
            kind, identifier);
        var service = await reader.HydrateAsync(new ClientService(request.TenantId,
            request.ServiceId), ct).ConfigureAwait(false);
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        return await executor.ExecuteAsync(new CommitmentDraft(request.TenantId, draftId),
            draft =>
            {
                if (!draft.IsCreated && (!service.IsActive ||
                                         service.ProgramId != request.ProgramId))
                    return AggregateOutcome.CommitOnSuccess(
                        Result<CommitmentDraftRegistration>.Failure(new RequestError(
                            RequestErrorKind.NotFound, "The program service was not found.")));
                return AggregateOutcome.CommitOnSuccess(draft.Create(request.ProgramId,
                    context.RequestId, request.ServiceId, kind, identifier, request.Statement,
                    request.Context, request.SourceReference,
                    RbacIds.Member(request.TenantId, userId),
                    UserIdentityClaims.BdgrzDisplay(context.Actor, userId), clock.GetUtcNow()));
            },
            context, ct).ConfigureAwait(false);
    }
}
