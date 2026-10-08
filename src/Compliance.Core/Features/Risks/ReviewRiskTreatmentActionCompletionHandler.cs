using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Bdgrz.Compliance.Features.Work;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     Independently accepts or rejects a pending treatment action completion. Acceptance
///     re-reads the evidence requests, so evidence that is no longer fulfilled cannot complete the
///     action. The Portia request ID identifies the decision.
/// </summary>
public sealed class ReviewRiskTreatmentActionCompletionHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<ReviewRiskTreatmentActionCompletion>
{
    public async ValueTask<Result> HandleAsync(
        IRequestContext<ReviewRiskTreatmentActionCompletion> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Risk sign-off requires a personal HTTP invocation."));
        var request = context.Request;
        var risk = await RiskOwnerResolution.RequireRiskAsync(reader, request.TenantId,
            request.ProgramId, request.RiskId, ct).ConfigureAwait(false);
        if (!risk.IsSuccess)
            return risk;
        var actor = RiskActor.From(context.Actor, request.TenantId);
        var governance = await reader.HydrateAsync(new RiskGovernanceLedger(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        var action = governance.Actions().FirstOrDefault(action =>
                action.RiskId == request.RiskId && action.ActionId == request.ActionId &&
                action.Status == RiskGovernanceLedger.ActionSubmitted);
        var pending = action?.Completions.LastOrDefault(static completion =>
            completion.ReviewOutcome is null);
        if (pending is not null)
        {
            var assignments = await reader.HydrateAsync(new WorkAssignmentLedger(request.TenantId,
                request.ProgramId), ct).ConfigureAwait(false);
            var workItemId = WorkSource.RiskTreatmentWorkItemId(request.ProgramId,
                request.RiskId, pending.SubmissionId, WorkSource.RiskTreatmentActionReview);
            var assigned = assignments.Read(workItemId).AssigneeMemberId == actor.MemberId;
            var conflicts = governance.SubmitterMemberId(request.RiskId, pending.SubmissionId) ==
                                actor.MemberId || action?.AccountableMemberId == actor.MemberId;
            var mayReviewByWaiver = request.SeparationOfDutiesWaiverId is not null && conflicts;
            if (!assigned && !mayReviewByWaiver)
                return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                    "Only the assigned reviewer may review this completion."));
        }
        SeparationOfDutiesWaiver? waiver = null;
        if (request.SeparationOfDutiesWaiverId is { } waiverId)
            waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId,
                waiverId), ct).ConfigureAwait(false);
        var fulfilled = await RiskActionEvidence.FulfilledAsync(reader, request.TenantId,
            request.ProgramId, ct).ConfigureAwait(false);
        return await executor.ExecuteAsync(new RiskGovernanceLedger(request.TenantId,
                request.ProgramId),
            ledger => CommandFailureRequestAdapter.ToOutcome(ledger.ReviewActionCompletion(
                request.RiskId, request.ActionId, request.ExpectedRevision, context.RequestId,
                request.Outcome, request.Rationale, actor.MemberId,
                ActorReference.ForMember(actor.MemberId, actor.Display), clock.GetUtcNow(),
                fulfilled, waiver)),
            context, ct).ConfigureAwait(false);
    }
}
