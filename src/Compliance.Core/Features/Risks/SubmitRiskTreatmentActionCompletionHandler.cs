using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     Records that treatment work reached its target state, citing fulfilled evidence requests.
///     The action stays incomplete until an independent review accepts it. The Portia request ID
///     identifies the submission.
/// </summary>
public sealed class SubmitRiskTreatmentActionCompletionHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<SubmitRiskTreatmentActionCompletion,
        RiskTreatmentActionCompletionRegistration>
{
    public async ValueTask<Result<RiskTreatmentActionCompletionRegistration>> HandleAsync(
        IRequestContext<SubmitRiskTreatmentActionCompletion> context, CancellationToken ct)
    {
        var request = context.Request;
        var risk = await RiskOwnerResolution.RequireRiskAsync(reader, request.TenantId,
            request.ProgramId, request.RiskId, ct).ConfigureAwait(false);
        if (!risk.IsSuccess)
            return Result<RiskTreatmentActionCompletionRegistration>.Failure(risk.Error);
        var fulfilled = await RiskActionEvidence.FulfilledAsync(reader, request.TenantId,
            request.ProgramId, ct).ConfigureAwait(false);
        var actor = RiskActor.From(context.Actor, request.TenantId);
        return await executor.ExecuteAsync(new RiskGovernanceLedger(request.TenantId,
                request.ProgramId),
            ledger => CommandFailureRequestAdapter.ToOutcome(
                ledger.SubmitActionCompletion(request.RiskId, request.ActionId,
                    request.ExpectedRevision, context.RequestId, request.Summary,
                    request.EvidenceRequestIds ?? [], fulfilled, actor.MemberId,
                    ActorReference.ForMember(actor.MemberId, actor.Display), clock.GetUtcNow()),
                new RiskTreatmentActionCompletionRegistration(context.RequestId,
                    ledger.RevisionOf(request.RiskId))), context, ct).ConfigureAwait(false);
    }
}
