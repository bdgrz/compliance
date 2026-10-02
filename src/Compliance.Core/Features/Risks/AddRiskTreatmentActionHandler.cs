using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     Adds accountable treatment work to a risk. The accountable member must be an active client
///     member and every named evidence request must be an uncancelled request in the program. The
///     Portia request ID identifies the action.
/// </summary>
public sealed class AddRiskTreatmentActionHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<AddRiskTreatmentAction, RiskTreatmentActionRegistration>
{
    public async ValueTask<Result<RiskTreatmentActionRegistration>> HandleAsync(
        IRequestContext<AddRiskTreatmentAction> context, CancellationToken ct)
    {
        var request = context.Request;
        var risk = await RiskOwnerResolution.RequireRiskAsync(reader, request.TenantId,
            request.ProgramId, request.RiskId, ct).ConfigureAwait(false);
        if (!risk.IsSuccess)
            return Result<RiskTreatmentActionRegistration>.Failure(risk.Error);
        if (!await RemediationCommands.IsActiveMemberAsync(reader, request.TenantId,
                request.AccountableMemberId, ct).ConfigureAwait(false))
            return Result<RiskTreatmentActionRegistration>.Failure(
                RemediationCommands.InactiveOwner());
        var evidence = await reader.HydrateAsync(new EvidenceRequestLedger(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        foreach (var id in request.EvidenceRequestIds ?? [])
            if (evidence.Find(id) is not { Status: not EvidenceRequestLedger.Cancelled })
                return Result<RiskTreatmentActionRegistration>.Failure(new RequestError(
                    RequestErrorKind.Validation,
                    "Every evidence request must be an open or fulfilled request in this program."));
        var treatment = (await reader.HydrateAsync(new RiskEvaluation(request.TenantId,
            request.RiskId), ct).ConfigureAwait(false)).Treatment?.Kind;
        var actor = RiskActor.From(context.Actor, request.TenantId);
        return await executor.ExecuteAsync(new RiskGovernanceLedger(request.TenantId,
                request.ProgramId),
            ledger => CommandFailureRequestAdapter.ToOutcome(
                ledger.AddTreatmentAction(request.RiskId, request.ExpectedRevision,
                    context.RequestId, treatment, request.Title, request.TargetState,
                    request.ExpectedEvidence, request.DueOn, request.AccountableMemberId,
                    request.EvidenceRequestIds,
                    ActorReference.ForMember(actor.MemberId, actor.Display), clock.GetUtcNow()),
                new RiskTreatmentActionRegistration(context.RequestId,
                    ledger.RevisionOf(request.RiskId))), context, ct).ConfigureAwait(false);
    }
}
