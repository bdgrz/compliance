using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public sealed class ApproveBoundaryHandler(IAggregateExecutor executor,
    BoundaryImpactService impact, IBoundaryDirectoryReader boundaries,
    IBoundaryReferenceValidator references, TimeProvider clock)
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
                "The impact preview is incomplete. Pending or incomplete contexts: " +
                string.Join(", ", preview.Value.PendingContexts)));
        if (!StringComparer.Ordinal.Equals(request.ImpactDigest, preview.Value.Digest))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The impact preview changed. Reload it before approval."));
        var boundaryView = await boundaries.GetAsync(request.TenantId, request.BoundaryId, ct)
            .ConfigureAwait(false);
        if (boundaryView?.Draft is not { } draft ||
            draft.VersionId != request.DraftVersionId || draft.Revision != request.ExpectedRevision)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The boundary draft changed. Reload it before approval."));
        var referenceValidation = await references.ValidateAsync(request.TenantId,
                boundaryView.ProgramId, draft.Content, ct)
            .ConfigureAwait(false);
        if (!referenceValidation.IsSuccess)
            return referenceValidation;
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        return await executor.ExecuteAsync(new SystemBoundary(request.TenantId, request.BoundaryId),
            boundary => CommandFailureRequestAdapter.ToOutcome(boundary.Approve(request.DraftVersionId,
                request.ExpectedRevision, context.RequestId, request.AcceptedReviewDecisionId,
                request.EffectiveFrom, request.Rationale, request.ImpactDigest,
                RbacIds.Member(request.TenantId, userId),
                UserIdentityClaims.BdgrzDisplay(context.Actor, userId), clock.GetUtcNow())),
            context, ct).ConfigureAwait(false);
    }
}
