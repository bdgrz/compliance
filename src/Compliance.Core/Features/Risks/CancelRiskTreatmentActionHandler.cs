using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>Records an attributable cancellation of unfinished risk treatment work.</summary>
public sealed class CancelRiskTreatmentActionHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<CancelRiskTreatmentAction>
{
    public async ValueTask<Result> HandleAsync(
        IRequestContext<CancelRiskTreatmentAction> context, CancellationToken ct)
    {
        var request = context.Request;
        var risk = await RiskOwnerResolution.RequireRiskAsync(reader, request.TenantId,
            request.ProgramId, request.RiskId, ct).ConfigureAwait(false);
        if (!risk.IsSuccess)
            return risk;

        var actor = RiskActor.From(context.Actor, request.TenantId);
        return await executor.ExecuteAsync(new RiskGovernanceLedger(request.TenantId,
                request.ProgramId),
            ledger => CommandFailureRequestAdapter.ToOutcome(ledger.CancelTreatmentAction(
                request.RiskId, request.ActionId, request.ExpectedRevision, context.RequestId,
                request.Rationale, ActorReference.ForMember(actor.MemberId, actor.Display),
                clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
