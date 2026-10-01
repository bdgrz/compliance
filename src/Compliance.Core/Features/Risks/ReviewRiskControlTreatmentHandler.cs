using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public sealed class ReviewRiskControlTreatmentHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock) : IRequestHandler<ReviewRiskControlTreatment>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ReviewRiskControlTreatment> context,
        CancellationToken ct)
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
        var actor = RiskActor.From(context.Actor, request.TenantId);
        return await executor.ExecuteAsync(new RiskGovernanceLedger(request.TenantId,
                request.ProgramId),
            ledger => CommandFailureRequestAdapter.ToOutcome(ledger.ReviewControlTreatment(
                request.RiskId, request.TreatmentId, request.ExpectedRevision, context.RequestId,
                request.Outcome, request.Rationale, actor.MemberId,
                ActorReference.ForMember(actor.MemberId, actor.Display), clock.GetUtcNow(),
                waiver)),
            context, ct).ConfigureAwait(false);
    }
}
