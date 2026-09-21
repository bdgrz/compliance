using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public sealed class CreateRiskDraftHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<CreateRiskDraft, RiskRegistration>
{
    public async ValueTask<Result<RiskRegistration>> HandleAsync(
        IRequestContext<CreateRiskDraft> context, CancellationToken ct)
    {
        var request = context.Request;
        var identifier = RiskDraft.NormalizeIdentifier(request.Identifier);
        if (identifier.Length is < 1 or > 80)
            return Result<RiskRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                "A risk identifier requires 1 to 80 characters."));
        var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        if (!program.IsCreated)
            return Result<RiskRegistration>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The program was not found."));
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        var riskId = RiskDraft.IdFor(request.TenantId, request.ProgramId, identifier);
        RiskDraftContent content = new(request.Title, request.Scenario,
            request.PotentialEffect, request.SourceNote);
        return await executor.ExecuteAsync(new RiskDraft(request.TenantId, riskId),
            risk => AggregateOutcome.CommitOnSuccess(risk.Create(request.ProgramId,
                context.RequestId, identifier, content,
                RbacIds.Member(request.TenantId, userId),
                UserIdentityClaims.BdgrzDisplay(context.Actor, userId), clock.GetUtcNow())),
            context, ct).ConfigureAwait(false);
    }
}

public sealed class ReviseRiskDraftHandler(IAggregateExecutor executor,
    TimeProvider clock) : IRequestHandler<ReviseRiskDraft>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviseRiskDraft> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        RiskDraftContent content = new(request.Title, request.Scenario,
            request.PotentialEffect, request.SourceNote);
        return executor.ExecuteAsync(new RiskDraft(request.TenantId, request.RiskId),
            risk => AggregateOutcome.CommitOnSuccess(risk.Revise(request.ProgramId,
                request.ExpectedRevision, content,
                RbacIds.Member(request.TenantId, userId),
                UserIdentityClaims.BdgrzDisplay(context.Actor, userId), clock.GetUtcNow())),
            context, ct);
    }
}

public sealed class RiskDraftReadConsistency(IRiskDraftDirectoryReader directory,
    IAggregateReader reader)
{
    public async ValueTask<Result<RiskDraftView>> GetAsync(Uuid tenantId, Uuid programId,
        Uuid riskId, long? minimumRevision, CancellationToken ct)
    {
        if (minimumRevision is < 1)
            return Result<RiskDraftView>.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum risk draft revision must be positive."));
        var source = await reader.HydrateAsync(new RiskDraft(tenantId, riskId), ct)
            .ConfigureAwait(false);
        if (!source.IsCreated || source.ProgramId != programId)
            return Result<RiskDraftView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The risk draft was not found."));
        var view = await directory.GetAsync(tenantId, riskId, ct).ConfigureAwait(false);
        if (view is not null && view.TenantId == tenantId && view.ProgramId == programId &&
            view.RiskId == riskId &&
            view.Revision >= source.Revision &&
            (minimumRevision is null || view.Revision >= minimumRevision))
            return Result<RiskDraftView>.Success(view);
        return Result<RiskDraftView>.Failure(new RequestError(RequestErrorKind.Conflict,
            minimumRevision is { } minimum && source.Revision < minimum
                ? $"The risk draft source has not reached revision {minimum}."
                : "The risk draft projection has not reached the requested revision.",
            isTransient: true));
    }
}

/// <summary>Checks the source area cursor before returning even an empty list.</summary>
public sealed class RiskDraftListReadConsistency(IRiskDraftDirectoryReader directory,
    IDomainEventReader events)
{
    public async ValueTask<Result> EnsureCaughtUpAsync(Uuid tenantId, CancellationToken ct)
    {
        var checkpoint = await directory.LoadCheckpointAsync(tenantId, ct)
            .ConfigureAwait(false);
        await using var pending = events.ReadAsync(
                EventStreamPattern.ForPattern(tenantId.ToString(), "risks"),
                checkpoint.Cursor, ct).GetAsyncEnumerator(ct);
        return await pending.MoveNextAsync().ConfigureAwait(false)
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The risk draft list projection has not reached the source.",
                isTransient: true))
            : Result.Success;
    }
}

public sealed class GetRiskDraftHandler(RiskDraftReadConsistency consistency)
    : IRequestHandler<GetRiskDraft, RiskDraftView>
{
    public ValueTask<Result<RiskDraftView>> HandleAsync(
        IRequestContext<GetRiskDraft> context, CancellationToken ct) =>
        consistency.GetAsync(context.Request.TenantId, context.Request.ProgramId,
            context.Request.RiskId, context.Request.MinimumRevision, ct);
}

public sealed class ListRiskDraftsHandler(IRiskDraftDirectoryReader directory,
    IAggregateReader reader, RiskDraftListReadConsistency consistency)
    : IRequestHandler<ListRiskDrafts, Page<RiskDraftView>>
{
    public async ValueTask<Result<Page<RiskDraftView>>> HandleAsync(
        IRequestContext<ListRiskDrafts> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<RiskDraftView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "Limit must be between 1 and 200."));
        var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        if (!program.IsCreated)
            return Result<Page<RiskDraftView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The program was not found."));
        var ready = await consistency.EnsureCaughtUpAsync(request.TenantId, ct)
            .ConfigureAwait(false);
        if (!ready.IsSuccess)
            return Result<Page<RiskDraftView>>.Failure(ready.Error);
        Page<RiskDraftView> page;
        try
        {
            page = await directory.ListProgramAsync(request.TenantId, request.ProgramId,
                request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<RiskDraftView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The risk draft cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId ||
                                      item.ProgramId != request.ProgramId)
            ? Result<Page<RiskDraftView>>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The risk draft projection has an invalid program scope."))
            : Result<Page<RiskDraftView>>.Success(page);
    }
}

public sealed class GetRiskDraftRevisionHandler(IRiskDraftDirectoryReader directory,
    RiskDraftReadConsistency consistency)
    : IRequestHandler<GetRiskDraftRevision, RiskDraftRevisionView>
{
    public async ValueTask<Result<RiskDraftRevisionView>> HandleAsync(
        IRequestContext<GetRiskDraftRevision> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Revision < 1)
            return Result<RiskDraftRevisionView>.Failure(new RequestError(
                RequestErrorKind.Validation, "The risk draft revision must be positive."));
        var freshness = await consistency.GetAsync(request.TenantId, request.ProgramId,
            request.RiskId, request.Revision, ct).ConfigureAwait(false);
        if (!freshness.IsSuccess)
            return Result<RiskDraftRevisionView>.Failure(freshness.Error);
        var revision = await directory.GetRevisionAsync(request.TenantId, request.RiskId,
            request.Revision, ct).ConfigureAwait(false);
        return revision is not null && revision.TenantId == request.TenantId &&
               revision.ProgramId == request.ProgramId && revision.RiskId == request.RiskId
            ? Result<RiskDraftRevisionView>.Success(revision)
            : Result<RiskDraftRevisionView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The risk draft revision projection is incomplete.", isTransient: true));
    }
}
