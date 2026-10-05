using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>
///     Starts an evaluation of the control version effective today. The control owner may evaluate
///     their own control (M0-D13); otherwise a program manager evaluates.
/// </summary>
public sealed class StartControlEvaluationHandler(IAggregateExecutor executor,
    IAggregateReader reader, OperatingAuthority authority, TimeProvider clock)
    : IRequestHandler<StartControlEvaluation, ControlEvaluationView>
{
    public async ValueTask<Result<ControlEvaluationView>> HandleAsync(
        IRequestContext<StartControlEvaluation> context, CancellationToken ct)
    {
        var request = context.Request;
        var control = await ControlOperationsSource.LoadControlAsync(reader, request.TenantId,
            request.ProgramId, request.ControlId, ct).ConfigureAwait(false);
        if (control is null)
            return Result<ControlEvaluationView>.Failure(ControlOperationsSource.ControlNotFound());
        var now = clock.GetUtcNow();
        if (control.EffectiveVersion(DateOnly.FromDateTime(now.UtcDateTime)) is not { } version)
            return Result<ControlEvaluationView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The control has no approved version effective today to evaluate."));
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        if (version.OwnerMemberId != actor.MemberId && !await authority.ManagesProgramAsync(
                request.TenantId, actor, request.ProgramId, ct).ConfigureAwait(false))
            return Result<ControlEvaluationView>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the control owner or a program manager may evaluate this control."));
        var plan = await reader.HydrateAsync(new ControlEvaluationPlan(request.TenantId,
            request.ProgramId, request.ControlId), ct).ConfigureAwait(false);
        var planVersion = plan.FindVersion(request.PlanVersionId);
        if (planVersion is null)
            return Result<ControlEvaluationView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The control evaluation plan version was not found."));
        if (planVersion.ControlVersionId != version.VersionId)
            return Result<ControlEvaluationView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The evaluation plan is bound to a different control version."));
        if (request.RetestOfEvaluationId is null &&
            planVersion.Version != plan.Revision)
            return Result<ControlEvaluationView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "A new evaluation must use the current control evaluation plan version."));
        if (planVersion.TesterIndependenceRequired && version.OwnerMemberId == actor.MemberId)
            return Result<ControlEvaluationView>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "This evaluation plan requires an evaluator independent of the control owner."));
        return await ControlEvaluationSource.ExecuteAsync(executor, context, request.TenantId,
            request.ProgramId, request.ControlId, context.RequestId,
            ledger => ledger.Start(request.ControlId, context.RequestId, version.VersionId,
                planVersion.PlanVersionId, planVersion.Version, planVersion.Steps,
                request.RetestOfEvaluationId, actor.MemberId, actor.Display, now),
            ct).ConfigureAwait(false);
    }
}
