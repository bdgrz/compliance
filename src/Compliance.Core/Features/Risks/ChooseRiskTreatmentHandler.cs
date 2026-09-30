using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public sealed class ChooseRiskTreatmentHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock) : IRequestHandler<ChooseRiskTreatment>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ChooseRiskTreatment> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var risk = await reader.HydrateAsync(new RiskDraft(request.TenantId, request.RiskId), ct)
            .ConfigureAwait(false);
        if (!risk.IsCreated || risk.ProgramId != request.ProgramId)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The risk was not found."));
        var actor = RiskActor.From(context.Actor, request.TenantId);
        return await executor.ExecuteAsync(new RiskEvaluation(request.TenantId, request.RiskId),
            evaluation => CommandFailureRequestAdapter.ToOutcome(evaluation.ChooseTreatment(
                request.ProgramId, request.ExpectedRevision, request.Kind, request.Rationale,
                actor.MemberId, actor.Display, clock.GetUtcNow())),
            context, ct).ConfigureAwait(false);
    }
}
