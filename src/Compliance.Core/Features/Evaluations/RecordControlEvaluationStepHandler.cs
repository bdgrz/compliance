using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>Records the evaluator's result for one frozen procedure step.</summary>
public sealed class RecordControlEvaluationStepHandler(IAggregateExecutor executor,
    TimeProvider clock)
    : IRequestHandler<RecordControlEvaluationStep, ControlEvaluationView>
{
    public async ValueTask<Result<ControlEvaluationView>> HandleAsync(
        IRequestContext<RecordControlEvaluationStep> context, CancellationToken ct)
    {
        var request = context.Request;
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var now = clock.GetUtcNow();
        return await ControlEvaluationSource.ExecuteAsync(executor, context, request.TenantId,
            request.ProgramId, request.ControlId, request.EvaluationId,
            ledger => ledger.RecordStep(request.ControlId, request.EvaluationId, request.StepId,
                request.ExpectedRevision, request.Result, request.Rationale,
                request.InspectedItems, request.DeviationClassification,
                request.DeviationDescription, actor.MemberId, actor.Display, now),
            ct).ConfigureAwait(false);
    }
}
