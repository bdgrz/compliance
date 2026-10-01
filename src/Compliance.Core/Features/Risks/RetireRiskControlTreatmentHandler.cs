using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public sealed class RetireRiskControlTreatmentHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock) : IRequestHandler<RetireRiskControlTreatment>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<RetireRiskControlTreatment> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var risk = await RiskOwnerResolution.RequireRiskAsync(reader, request.TenantId,
            request.ProgramId, request.RiskId, ct).ConfigureAwait(false);
        if (!risk.IsSuccess)
            return risk;
        var actor = RiskActor.From(context.Actor, request.TenantId);
        return await executor.ExecuteAsync(new RiskGovernanceLedger(request.TenantId,
                request.ProgramId),
            ledger => CommandFailureRequestAdapter.ToOutcome(ledger.RetireControlTreatment(
                request.RiskId, request.TreatmentId, request.ExpectedRevision, request.Rationale,
                ActorReference.ForMember(actor.MemberId, actor.Display), clock.GetUtcNow())),
            context, ct).ConfigureAwait(false);
    }
}
