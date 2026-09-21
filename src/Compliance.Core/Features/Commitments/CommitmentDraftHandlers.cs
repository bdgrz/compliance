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

public sealed class CommitmentDraftReadConsistency(ICommitmentDraftDirectoryReader directory,
    IAggregateReader reader)
{
    public async ValueTask<Result<CommitmentDraftView>> GetAsync(Uuid tenantId, Uuid programId,
        Uuid draftId, long? minimumRevision, CancellationToken ct)
    {
        if (minimumRevision is < 1)
            return Result<CommitmentDraftView>.Failure(new RequestError(
                RequestErrorKind.Validation, "The minimum draft revision must be positive."));
        var source = await reader.HydrateAsync(new CommitmentDraft(tenantId, draftId), ct)
            .ConfigureAwait(false);
        if (!source.IsCreated || source.ProgramId != programId)
            return Result<CommitmentDraftView>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The draft was not found."));
        var view = await directory.GetAsync(tenantId, draftId, ct).ConfigureAwait(false);
        if (view is not null && view.TenantId == tenantId && view.ProgramId == programId &&
            view.DraftId == draftId && view.Revision >= source.Revision &&
            (minimumRevision is null || view.Revision >= minimumRevision))
            return Result<CommitmentDraftView>.Success(view);
        return Result<CommitmentDraftView>.Failure(new RequestError(RequestErrorKind.Conflict,
            minimumRevision is { } minimum && source.Revision < minimum
                ? $"The draft source has not reached revision {minimum}."
                : "The draft projection has not reached the requested revision.",
            isTransient: true));
    }
}

public sealed class CommitmentDraftListReadConsistency(ICommitmentDraftDirectoryReader directory,
    IDomainEventReader events)
{
    public async ValueTask<Result> EnsureCaughtUpAsync(Uuid tenantId, CancellationToken ct)
    {
        var checkpoint = await directory.LoadCheckpointAsync(tenantId, ct)
            .ConfigureAwait(false);
        await using var pending = events.ReadAsync(
                EventStreamPattern.ForPattern(tenantId.ToString(), "commitment-drafts"),
                checkpoint.Cursor, ct).GetAsyncEnumerator(ct);
        return await pending.MoveNextAsync().ConfigureAwait(false)
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The draft list projection has not reached the source.", isTransient: true))
            : Result.Success;
    }
}

public sealed class GetCommitmentDraftHandler(CommitmentDraftReadConsistency consistency)
    : IRequestHandler<GetCommitmentDraft, CommitmentDraftView>
{
    public ValueTask<Result<CommitmentDraftView>> HandleAsync(
        IRequestContext<GetCommitmentDraft> context, CancellationToken ct) =>
        consistency.GetAsync(context.Request.TenantId, context.Request.ProgramId,
            context.Request.DraftId, context.Request.MinimumRevision, ct);
}

public sealed class ListCommitmentDraftsHandler(ICommitmentDraftDirectoryReader directory,
    IAggregateReader reader, CommitmentDraftListReadConsistency consistency)
    : IRequestHandler<ListCommitmentDrafts, Page<CommitmentDraftView>>
{
    public async ValueTask<Result<Page<CommitmentDraftView>>> HandleAsync(
        IRequestContext<ListCommitmentDrafts> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<CommitmentDraftView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "Limit must be between 1 and 200."));
        var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        if (!program.IsCreated)
            return Result<Page<CommitmentDraftView>>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The program was not found."));
        var ready = await consistency.EnsureCaughtUpAsync(request.TenantId, ct)
            .ConfigureAwait(false);
        if (!ready.IsSuccess)
            return Result<Page<CommitmentDraftView>>.Failure(ready.Error);
        Page<CommitmentDraftView> page;
        try
        {
            page = await directory.ListProgramAsync(request.TenantId, request.ProgramId,
                request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<CommitmentDraftView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The draft cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId ||
                                      item.ProgramId != request.ProgramId)
            ? Result<Page<CommitmentDraftView>>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The draft projection has an invalid program scope."))
            : Result<Page<CommitmentDraftView>>.Success(page);
    }
}

public sealed class GetCommitmentDraftRevisionHandler(
    ICommitmentDraftDirectoryReader directory, CommitmentDraftReadConsistency consistency)
    : IRequestHandler<GetCommitmentDraftRevision, CommitmentDraftRevisionView>
{
    public async ValueTask<Result<CommitmentDraftRevisionView>> HandleAsync(
        IRequestContext<GetCommitmentDraftRevision> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Revision < 1)
            return Result<CommitmentDraftRevisionView>.Failure(new RequestError(
                RequestErrorKind.Validation, "The draft revision must be positive."));
        var freshness = await consistency.GetAsync(request.TenantId, request.ProgramId,
            request.DraftId, request.Revision, ct).ConfigureAwait(false);
        if (!freshness.IsSuccess)
            return Result<CommitmentDraftRevisionView>.Failure(freshness.Error);
        var revision = await directory.GetRevisionAsync(request.TenantId, request.DraftId,
            request.Revision, ct).ConfigureAwait(false);
        return revision is not null && revision.TenantId == request.TenantId &&
               revision.ProgramId == request.ProgramId && revision.DraftId == request.DraftId
            ? Result<CommitmentDraftRevisionView>.Success(revision)
            : Result<CommitmentDraftRevisionView>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The draft revision projection is incomplete.",
                isTransient: true));
    }
}
