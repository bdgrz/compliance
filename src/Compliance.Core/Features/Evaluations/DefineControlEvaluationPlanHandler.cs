using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>Defines a new immutable evaluation plan version for an approved control.</summary>
public sealed class DefineControlEvaluationPlanHandler(IAggregateExecutor executor,
    IAggregateReader reader, OperatingAuthority authority, TimeProvider clock)
    : IRequestHandler<DefineControlEvaluationPlan, ControlEvaluationPlanVersionView>
{
    public async ValueTask<Result<ControlEvaluationPlanVersionView>> HandleAsync(
        IRequestContext<DefineControlEvaluationPlan> context, CancellationToken ct)
    {
        var request = context.Request;
        var control = await ControlOperationsSource.LoadControlAsync(reader, request.TenantId,
            request.ProgramId, request.ControlId, ct).ConfigureAwait(false);
        if (control is null)
            return Result<ControlEvaluationPlanVersionView>.Failure(
                ControlOperationsSource.ControlNotFound());
        if (!control.IsApproved || control.IsRetired)
            return Result<ControlEvaluationPlanVersionView>.Failure(new RequestError(
                RequestErrorKind.Conflict,
                "An evaluation plan requires a non-retired control with an approved version."));
        if (!control.ReadVersions().Any(version => version.VersionId == request.ControlVersionId))
            return Result<ControlEvaluationPlanVersionView>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The approved control version was not found."));

        var actor = OperationsActor.From(context.Actor, request.TenantId);
        if (!await authority.ManagesProgramAsync(request.TenantId, actor, request.ProgramId, ct)
                .ConfigureAwait(false))
            return Result<ControlEvaluationPlanVersionView>.Failure(new RequestError(
                RequestErrorKind.Forbidden,
                "Only a program manager may define a control evaluation plan."));

        return await executor.ExecuteAsync(new ControlEvaluationPlan(request.TenantId,
                request.ProgramId, request.ControlId), plan =>
            {
                var failure = plan.Define(request.ExpectedVersion, request.ControlVersionId,
                    context.RequestId,
                    request.Objective, request.Steps, request.TesterIndependenceRequired,
                    actor.MemberId, ActorReference.ForMember(actor.MemberId, actor.Display),
                    clock.GetUtcNow());
                return CommandFailureRequestAdapter.ToOutcome(failure,
                    plan.FindVersion(context.RequestId)!);
            }, context, ct).ConfigureAwait(false);
    }
}
