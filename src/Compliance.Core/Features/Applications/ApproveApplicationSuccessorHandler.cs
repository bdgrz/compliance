using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Approves a proposed successor only from a personal HTTP request and current impact.</summary>
public sealed class ApproveApplicationSuccessorHandler(IAggregateExecutor executor,
    IAggregateReader reader, IApplicationChangeImpactReader impactReader, TimeProvider clock)
    : IRequestHandler<ApproveApplicationSuccessor>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ApproveApplicationSuccessor> context,
        CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Successor approval requires an authenticated member's personal HTTP request."));

        var request = context.Request;
        if (request.PredecessorApplicationId == Uuid.Empty ||
            request.SuccessorApplicationId == Uuid.Empty ||
            request.PredecessorApplicationId == request.SuccessorApplicationId ||
            request.ExpectedSourceRevision < 1 || request.ExpectedTargetRevision < 1 ||
            request.ExpectedRelationshipRevision < 1 ||
            !ApplicationRelationshipIdentity.IsImpactDigest(request.ImpactDigest))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The successor approval request is invalid."));

        var source = await reader.HydrateApplicationAsync(request.TenantId,
            request.PredecessorApplicationId, ct).ConfigureAwait(false);
        if (!source.IsCreated)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The predecessor application was not found."));
        if (source.IsRetired || source.Revision != request.ExpectedSourceRevision)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The predecessor application changed. Reload before approval."));

        var successor = await reader.HydrateApplicationAsync(request.TenantId,
            request.SuccessorApplicationId, ct).ConfigureAwait(false);
        if (!successor.IsCreated || successor.IsRetired)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The successor application is unavailable or retired."));
        if (successor.Revision != request.ExpectedTargetRevision)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The successor application changed. Reload before approval."));

        var proposed = source.Relationship(ApplicationRelationshipIdentity.Replaces,
            request.SuccessorApplicationId);
        if (!IsCurrentProposal(proposed, request))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The proposed successor relationship changed. Reload before approval."));

        var assessment = await impactReader.ReadAsync(new PreviewApplicationChange(
            request.TenantId, request.PredecessorApplicationId,
            request.ExpectedSourceRevision, "retire"), userId, ct).ConfigureAwait(false);
        if (!assessment.IsSuccess)
            return Result.Failure(assessment.Error);
        if (!assessment.Value.Complete || assessment.Value.PendingContexts.Count != 0)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Successor approval is blocked until every impact context is current and complete."));
        if (!string.Equals(assessment.Value.ImpactDigest, request.ImpactDigest,
                StringComparison.Ordinal))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The impact evidence changed. Reload the preview before approval."));

        var currentSource = await reader.HydrateApplicationAsync(request.TenantId,
            request.PredecessorApplicationId, ct).ConfigureAwait(false);
        var currentSuccessor = await reader.HydrateApplicationAsync(request.TenantId,
            request.SuccessorApplicationId, ct).ConfigureAwait(false);
        var currentProposal = currentSource.Relationship(ApplicationRelationshipIdentity.Replaces,
            request.SuccessorApplicationId);
        if (currentSource.Revision != request.ExpectedSourceRevision ||
            !currentSuccessor.IsCreated || currentSuccessor.IsRetired ||
            currentSuccessor.Revision != request.ExpectedTargetRevision ||
            !IsCurrentProposal(currentProposal, request))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The applications or proposed relationship changed during approval."));

        var (memberId, display) = ApplicationActor.From(context);
        var target = await ApplicationImportWriteGuard.PrepareAsync(reader, request.TenantId,
            request.PredecessorApplicationId, ct).ConfigureAwait(false);
        return await executor.ExecuteAsync(target,
            application => application.CheckPendingImportChanges() is { } importError
                ? AggregateOutcome.Discard(Result.Failure(importError))
                : CommandFailureRequestAdapter.ToOutcome(application.ApproveSuccessor(
                    request.SuccessorApplicationId, request.ExpectedSourceRevision,
                    request.ExpectedRelationshipRevision, request.ExpectedTargetRevision,
                    assessment.Value.ImpactDigest, memberId, display, clock.GetUtcNow())),
            context, ct).ConfigureAwait(false);
    }

    static bool IsCurrentProposal(ApplicationRelationshipView? relationship,
        ApproveApplicationSuccessor request) => relationship is
        {
            Status: "proposed",
        } && relationship.Revision == request.ExpectedRelationshipRevision &&
        relationship.SourceApplicationRevision == request.ExpectedSourceRevision &&
        relationship.TargetApplicationRevision == request.ExpectedTargetRevision;
}
