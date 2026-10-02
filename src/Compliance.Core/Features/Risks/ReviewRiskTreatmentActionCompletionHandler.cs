using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
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
        var request = context.Request;
        var risk = await RiskOwnerResolution.RequireRiskAsync(reader, request.TenantId,
            request.ProgramId, request.RiskId, ct).ConfigureAwait(false);
        if (!risk.IsSuccess)
            return risk;
        SeparationOfDutiesWaiver? waiver = null;
        if (request.SeparationOfDutiesWaiverId is { } waiverId)
            waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId,
                waiverId), ct).ConfigureAwait(false);
        var fulfilled = await RiskActionEvidence.FulfilledAsync(reader, request.TenantId,
            request.ProgramId, ct).ConfigureAwait(false);
        var actor = RiskActor.From(context.Actor, request.TenantId);
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
