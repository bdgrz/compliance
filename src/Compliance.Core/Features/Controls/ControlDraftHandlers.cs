using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public sealed class CreateControlDraftHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<CreateControlDraft, ControlRegistration>
{
    public async ValueTask<Result<ControlRegistration>> HandleAsync(
        IRequestContext<CreateControlDraft> context, CancellationToken ct)
    {
        var request = context.Request;
        var identifier = ControlDraft.NormalizeIdentifier(request.Identifier);
        if (identifier.Length is < 1 or > 80)
            return Result<ControlRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                "A control identifier requires 1 to 80 characters."));
        var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        if (!program.IsCreated)
            return Result<ControlRegistration>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The program was not found."));
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        var controlId = ControlDraft.IdFor(request.TenantId, request.ProgramId, identifier);
        return await executor.ExecuteAsync(new ControlDraft(request.TenantId, controlId),
            control => AggregateOutcome.CommitOnSuccess(control.Create(request.ProgramId,
                context.RequestId, identifier, request.Content,
                RbacIds.Member(request.TenantId, userId),
                UserIdentityClaims.BdgrzDisplay(context.Actor, userId), clock.GetUtcNow())),
            context, ct).ConfigureAwait(false);
    }
}

public sealed class ReviseControlDraftHandler(IAggregateExecutor executor,
    TimeProvider clock) : IRequestHandler<ReviseControlDraft>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviseControlDraft> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        return executor.ExecuteAsync(new ControlDraft(request.TenantId, request.ControlId),
            control => AggregateOutcome.CommitOnSuccess(control.Revise(request.ProgramId,
                request.ExpectedRevision, request.Content,
                RbacIds.Member(request.TenantId, userId),
                UserIdentityClaims.BdgrzDisplay(context.Actor, userId), clock.GetUtcNow())),
            context, ct);
    }
}

public sealed class ControlDraftReadConsistency(IControlDraftDirectoryReader directory,
    IAggregateReader reader)
{
    public async ValueTask<Result<ControlDraftView>> GetAsync(Uuid tenantId, Uuid programId,
        Uuid controlId, long? minimumRevision, CancellationToken ct)
    {
        if (minimumRevision is < 1)
            return Result<ControlDraftView>.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum control draft revision must be positive."));
        var source = await reader.HydrateAsync(new ControlDraft(tenantId, controlId), ct)
            .ConfigureAwait(false);
        if (!source.IsCreated || source.ProgramId != programId)
            return Result<ControlDraftView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The control draft was not found."));
        var view = await directory.GetAsync(tenantId, controlId, ct).ConfigureAwait(false);
        if (view is not null && view.TenantId == tenantId && view.ProgramId == programId &&
            view.ControlId == controlId &&
            view.Revision >= source.Revision &&
            (minimumRevision is null || view.Revision >= minimumRevision))
            return Result<ControlDraftView>.Success(view);
        return Result<ControlDraftView>.Failure(new RequestError(RequestErrorKind.Conflict,
            minimumRevision is { } minimum && source.Revision < minimum
                ? $"The control draft source has not reached revision {minimum}."
                : "The control draft projection has not reached the requested revision.",
            isTransient: true));
    }
}

/// <summary>Checks the source area cursor before returning even an empty list.</summary>
public sealed class ControlDraftListReadConsistency(IControlDraftDirectoryReader directory,
    IDomainEventReader events)
{
    public async ValueTask<Result> EnsureCaughtUpAsync(Uuid tenantId, CancellationToken ct)
    {
        var checkpoint = await directory.LoadCheckpointAsync(tenantId, ct)
            .ConfigureAwait(false);
        await using var pending = events.ReadAsync(
                EventStreamPattern.ForPattern(tenantId.ToString(), "controls"),
                checkpoint.Cursor, ct).GetAsyncEnumerator(ct);
        return await pending.MoveNextAsync().ConfigureAwait(false)
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The control draft list projection has not reached the source.",
                isTransient: true))
            : Result.Success;
    }
}

public sealed class GetControlDraftHandler(ControlDraftReadConsistency consistency)
    : IRequestHandler<GetControlDraft, ControlDraftView>
{
    public ValueTask<Result<ControlDraftView>> HandleAsync(
        IRequestContext<GetControlDraft> context, CancellationToken ct) =>
        consistency.GetAsync(context.Request.TenantId, context.Request.ProgramId,
            context.Request.ControlId, context.Request.MinimumRevision, ct);
}

public sealed class ListControlDraftsHandler(IControlDraftDirectoryReader directory,
    IAggregateReader reader, ControlDraftListReadConsistency consistency)
    : IRequestHandler<ListControlDrafts, Page<ControlDraftView>>
{
    public async ValueTask<Result<Page<ControlDraftView>>> HandleAsync(
        IRequestContext<ListControlDrafts> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ControlDraftView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "Limit must be between 1 and 200."));
        var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        if (!program.IsCreated)
            return Result<Page<ControlDraftView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The program was not found."));
        var ready = await consistency.EnsureCaughtUpAsync(request.TenantId, ct)
            .ConfigureAwait(false);
        if (!ready.IsSuccess)
            return Result<Page<ControlDraftView>>.Failure(ready.Error);
        Page<ControlDraftView> page;
        try
        {
            page = await directory.ListProgramAsync(request.TenantId, request.ProgramId,
                request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ControlDraftView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The control draft cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId ||
                                      item.ProgramId != request.ProgramId)
            ? Result<Page<ControlDraftView>>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The control draft projection has an invalid program scope."))
            : Result<Page<ControlDraftView>>.Success(page);
    }
}

public sealed class GetControlDraftRevisionHandler(IControlDraftDirectoryReader directory,
    ControlDraftReadConsistency consistency)
    : IRequestHandler<GetControlDraftRevision, ControlDraftRevisionView>
{
    public async ValueTask<Result<ControlDraftRevisionView>> HandleAsync(
        IRequestContext<GetControlDraftRevision> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Revision < 1)
            return Result<ControlDraftRevisionView>.Failure(new RequestError(
                RequestErrorKind.Validation, "The control draft revision must be positive."));
        var freshness = await consistency.GetAsync(request.TenantId, request.ProgramId,
            request.ControlId, request.Revision, ct).ConfigureAwait(false);
        if (!freshness.IsSuccess)
            return Result<ControlDraftRevisionView>.Failure(freshness.Error);
        var revision = await directory.GetRevisionAsync(request.TenantId, request.ControlId,
            request.Revision, ct).ConfigureAwait(false);
        return revision is not null && revision.TenantId == request.TenantId &&
               revision.ProgramId == request.ProgramId && revision.ControlId == request.ControlId
            ? Result<ControlDraftRevisionView>.Success(revision)
            : Result<ControlDraftRevisionView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The control draft revision projection is incomplete.", isTransient: true));
    }
}
