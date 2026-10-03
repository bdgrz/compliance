using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     Revises an open treatment action. Its accountable member must be active and every named
///     evidence request must belong to the program and remain uncancelled.
/// </summary>
public sealed class ReviseRiskTreatmentActionHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock) : IRequestHandler<ReviseRiskTreatmentAction>
{
    public async ValueTask<Result> HandleAsync(
        IRequestContext<ReviseRiskTreatmentAction> context, CancellationToken ct)
    {
        var request = context.Request;
        var risk = await RiskOwnerResolution.RequireRiskAsync(reader, request.TenantId,
            request.ProgramId, request.RiskId, ct).ConfigureAwait(false);
        if (!risk.IsSuccess)
            return risk;
        var currentLedger = await reader.HydrateAsync(new RiskGovernanceLedger(
            request.TenantId, request.ProgramId), ct).ConfigureAwait(false);
        var replayFailure = currentLedger.CheckTreatmentActionEditReplay(request.RiskId,
            request.ActionId, request.ExpectedRevision, context.RequestId, request.Title,
            request.TargetState, request.ExpectedEvidence, request.DueOn,
            request.AccountableMemberId, request.EvidenceRequestIds, out var replayed);
        if (replayed)
            return replayFailure is null
                ? Result.Success
                : Result.Failure(CommandFailureRequestAdapter.ToRequestError(replayFailure));
        if (!await RemediationCommands.IsActiveMemberAsync(reader, request.TenantId,
                request.AccountableMemberId, ct).ConfigureAwait(false))
            return Result.Failure(RemediationCommands.InactiveOwner());
        var evidence = await reader.HydrateAsync(new EvidenceRequestLedger(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        foreach (var id in request.EvidenceRequestIds ?? [])
            if (evidence.Find(id) is not { Status: not EvidenceRequestLedger.Cancelled })
                return Result.Failure(new RequestError(RequestErrorKind.Validation,
                    "Every evidence request must be an open or fulfilled request in this program."));

        var actor = RiskActor.From(context.Actor, request.TenantId);
        return await executor.ExecuteAsync(new RiskGovernanceLedger(request.TenantId,
                request.ProgramId),
            ledger => CommandFailureRequestAdapter.ToOutcome(ledger.ReviseTreatmentAction(
                request.RiskId, request.ActionId, request.ExpectedRevision, context.RequestId,
                request.Title, request.TargetState, request.ExpectedEvidence, request.DueOn,
                request.AccountableMemberId, request.EvidenceRequestIds,
                ActorReference.ForMember(actor.MemberId, actor.Display), clock.GetUtcNow())),
            context, ct).ConfigureAwait(false);
    }
}
