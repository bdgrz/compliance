using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public sealed class RecordRiskAssessmentHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<RecordRiskAssessment, RiskAssessmentView>
{
    public async ValueTask<Result<RiskAssessmentView>> HandleAsync(
        IRequestContext<RecordRiskAssessment> context, CancellationToken ct)
    {
        var request = context.Request;
        var risk = await reader.HydrateAsync(new RiskDraft(request.TenantId, request.RiskId), ct)
            .ConfigureAwait(false);
        if (!risk.IsCreated || risk.ProgramId != request.ProgramId)
            return Result<RiskAssessmentView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The risk was not found."));
        var methods = await reader.HydrateAsync(new RiskMethod(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        if (methods.GetVersion(request.MethodVersion) is not { } method)
            return Result<RiskAssessmentView>.Failure(new RequestError(
                RequestErrorKind.Validation, "The risk method version was not found."));
        var governance = await reader.HydrateAsync(new RiskGovernanceLedger(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        var hasControlTreatment = governance.HasAcceptedControlTreatment(request.RiskId);
        var actor = RiskActor.From(context.Actor, request.TenantId);
        return await executor.ExecuteAsync(new RiskEvaluation(request.TenantId, request.RiskId),
            evaluation =>
            {
                var failure = evaluation.RecordAssessment(request.ProgramId,
                    request.ExpectedRevision, context.RequestId, method, request.Phase,
                    request.Likelihood, request.Impact, request.Rationale, actor.MemberId,
                    actor.Display, clock.GetUtcNow(), hasControlTreatment);
                return CommandFailureRequestAdapter.ToOutcome(failure,
                    evaluation.FindAssessment(context.RequestId)!);
            }, context, ct).ConfigureAwait(false);
    }
}
