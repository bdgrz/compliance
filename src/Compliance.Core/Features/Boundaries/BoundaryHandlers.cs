using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public sealed class CreateBoundaryHandler(IAggregateExecutor executor,
    IProgramDirectoryReader programs, IBoundaryReferenceValidator references,
    TimeProvider clock)
    : IRequestHandler<CreateBoundary, BoundaryRegistration>
{
    public async ValueTask<Result<BoundaryRegistration>> HandleAsync(
        IRequestContext<CreateBoundary> context, CancellationToken ct)
    {
        var request = context.Request;
        if (await programs.GetAsync(request.TenantId, request.ProgramId, ct).ConfigureAwait(false)
            is null)
            return Result<BoundaryRegistration>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The program was not found."));
        var validation = await references.ValidateAsync(request.TenantId, request.Content, ct)
            .ConfigureAwait(false);
        if (!validation.IsSuccess)
            return Result<BoundaryRegistration>.Failure(validation.Error);
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        var boundaryId = context.RequestId;
        var draftVersionId = Uuid.CreateVersion5(boundaryId, "draft-1");
        return await executor.ExecuteAsync(new SystemBoundary(request.TenantId, boundaryId),
            boundary => AggregateOutcome.CommitOnSuccess(boundary.Create(request.ProgramId,
                draftVersionId, request.Content, RbacIds.Member(request.TenantId, userId),
                context.Actor.FindFirst("email")?.Value ?? userId.ToString(), clock.GetUtcNow())),
            context, ct).ConfigureAwait(false);
    }
}

public sealed class ReviseBoundaryDraftHandler(IAggregateExecutor executor,
    IBoundaryReferenceValidator references, TimeProvider clock)
    : IRequestHandler<ReviseBoundaryDraft>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ReviseBoundaryDraft> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var validation = await references.ValidateAsync(request.TenantId, request.Content, ct)
            .ConfigureAwait(false);
        if (!validation.IsSuccess)
            return validation;
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        return await executor.ExecuteAsync(new SystemBoundary(request.TenantId, request.BoundaryId),
            boundary => AggregateOutcome.CommitOnSuccess(boundary.Revise(request.DraftVersionId,
                request.ExpectedRevision, request.Content,
                RbacIds.Member(request.TenantId, userId),
                context.Actor.FindFirst("email")?.Value ?? userId.ToString(), clock.GetUtcNow())),
            context, ct).ConfigureAwait(false);
    }
}

public sealed class GetBoundaryHandler(IBoundaryDirectoryReader directory)
    : IRequestHandler<GetBoundary, BoundaryView>
{
    public async ValueTask<Result<BoundaryView>> HandleAsync(IRequestContext<GetBoundary> context,
        CancellationToken ct)
    {
        var view = await directory.GetAsync(context.Request.TenantId,
            context.Request.BoundaryId, ct).ConfigureAwait(false);
        return view is null
            ? Result<BoundaryView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The boundary was not found."))
            : Result<BoundaryView>.Success(view);
    }
}

public sealed class GetBoundaryVersionHandler(IBoundaryDirectoryReader directory)
    : IRequestHandler<GetBoundaryVersion, BoundaryVersionView>
{
    public async ValueTask<Result<BoundaryVersionView>> HandleAsync(
        IRequestContext<GetBoundaryVersion> context, CancellationToken ct)
    {
        var request = context.Request;
        var version = await directory.GetVersionAsync(request.TenantId, request.BoundaryId,
            request.VersionId, ct).ConfigureAwait(false);
        return version is null
            ? Result<BoundaryVersionView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The boundary version was not found."))
            : Result<BoundaryVersionView>.Success(version);
    }
}

public sealed class ListBoundaryVersionsHandler(IBoundaryDirectoryReader directory)
    : IRequestHandler<ListBoundaryVersions, Page<BoundaryVersionView>>
{
    public async ValueTask<Result<Page<BoundaryVersionView>>> HandleAsync(
        IRequestContext<ListBoundaryVersions> context, CancellationToken ct)
    {
        var request = context.Request;
        var page = await directory.ListVersionsAsync(request.TenantId,
            request.BoundaryId, request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        return page is null
            ? Result<Page<BoundaryVersionView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The boundary was not found."))
            : Result<Page<BoundaryVersionView>>.Success(page);
    }
}

public sealed class GetEffectiveBoundaryVersionHandler(IBoundaryDirectoryReader directory)
    : IRequestHandler<GetEffectiveBoundaryVersion, BoundaryVersionView>
{
    public async ValueTask<Result<BoundaryVersionView>> HandleAsync(
        IRequestContext<GetEffectiveBoundaryVersion> context, CancellationToken ct)
    {
        var request = context.Request;
        var version = await directory.GetEffectiveVersionAsync(request.TenantId,
            request.BoundaryId, request.EffectiveOn, ct).ConfigureAwait(false);
        return version is null
            ? Result<BoundaryVersionView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "No approved boundary version was effective on that date."))
            : Result<BoundaryVersionView>.Success(version);
    }
}

public sealed class GetBoundaryDecisionHandler(IBoundaryDirectoryReader directory)
    : IRequestHandler<GetBoundaryDecision, BoundaryDecisionView>
{
    public async ValueTask<Result<BoundaryDecisionView>> HandleAsync(
        IRequestContext<GetBoundaryDecision> context, CancellationToken ct)
    {
        var request = context.Request;
        var decision = await directory.GetDecisionAsync(request.TenantId,
            request.BoundaryId, request.DecisionId, ct).ConfigureAwait(false);
        return decision is null
            ? Result<BoundaryDecisionView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The boundary decision was not found."))
            : Result<BoundaryDecisionView>.Success(decision);
    }
}

public sealed class ListBoundaryDecisionsHandler(IBoundaryDirectoryReader directory)
    : IRequestHandler<ListBoundaryDecisions, Page<BoundaryDecisionView>>
{
    public async ValueTask<Result<Page<BoundaryDecisionView>>> HandleAsync(
        IRequestContext<ListBoundaryDecisions> context, CancellationToken ct)
    {
        var request = context.Request;
        var page = await directory.ListDecisionsAsync(request.TenantId,
            request.BoundaryId, request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        return page is null
            ? Result<Page<BoundaryDecisionView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The boundary was not found."))
            : Result<Page<BoundaryDecisionView>>.Success(page);
    }
}

public sealed class ReviewBoundaryHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<ReviewBoundary>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviewBoundary> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        return executor.ExecuteAsync(new SystemBoundary(request.TenantId, request.BoundaryId),
            boundary => AggregateOutcome.CommitOnSuccess(boundary.Review(request.DraftVersionId,
                request.ExpectedRevision, context.RequestId, request.Outcome, request.Rationale,
                RbacIds.Member(request.TenantId, userId),
                context.Actor.FindFirst("email")?.Value ?? userId.ToString(), clock.GetUtcNow())),
            context, ct);
    }
}

public sealed class ApproveBoundaryHandler(IAggregateExecutor executor,
    BoundaryImpactService impact, TimeProvider clock)
    : IRequestHandler<ApproveBoundary>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ApproveBoundary> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var preview = await impact.PreviewAsync(new PreviewBoundaryImpact(request.TenantId,
            request.BoundaryId, request.DraftVersionId, request.ExpectedRevision), ct)
            .ConfigureAwait(false);
        if (!preview.IsSuccess)
            return Result.Failure(preview.Error);
        if (!preview.Value.Complete)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The impact preview is incomplete. Pending contexts: " +
                string.Join(", ", preview.Value.PendingContexts)));
        if (!StringComparer.Ordinal.Equals(request.ImpactDigest, preview.Value.Digest))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The impact preview changed. Reload it before approval."));
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        return await executor.ExecuteAsync(new SystemBoundary(request.TenantId, request.BoundaryId),
            boundary => AggregateOutcome.CommitOnSuccess(boundary.Approve(request.DraftVersionId,
                request.ExpectedRevision, context.RequestId, request.AcceptedReviewDecisionId,
                request.EffectiveFrom, request.Rationale, request.ImpactDigest,
                RbacIds.Member(request.TenantId, userId),
                context.Actor.FindFirst("email")?.Value ?? userId.ToString(), clock.GetUtcNow())),
            context, ct).ConfigureAwait(false);
    }
}

public sealed class ProposeBoundarySuccessorHandler(IAggregateExecutor executor,
    IBoundaryReferenceValidator references, TimeProvider clock)
    : IRequestHandler<ProposeBoundarySuccessor, BoundaryRegistration>
{
    public async ValueTask<Result<BoundaryRegistration>> HandleAsync(
        IRequestContext<ProposeBoundarySuccessor> context, CancellationToken ct)
    {
        var request = context.Request;
        var validation = await references.ValidateAsync(request.TenantId, request.Content, ct)
            .ConfigureAwait(false);
        if (!validation.IsSuccess)
            return Result<BoundaryRegistration>.Failure(validation.Error);
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        var draftVersionId = Uuid.CreateVersion5(context.RequestId, "draft");
        return await executor.ExecuteAsync(new SystemBoundary(request.TenantId, request.BoundaryId),
            boundary => AggregateOutcome.CommitOnSuccess(boundary.ProposeSuccessor(
                request.ExpectedApprovedVersionId, draftVersionId, request.Content,
                RbacIds.Member(request.TenantId, userId),
                context.Actor.FindFirst("email")?.Value ?? userId.ToString(), clock.GetUtcNow())),
            context, ct).ConfigureAwait(false);
    }
}
