using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>Records the evaluator's personal sign-off of separate assertion conclusions; HTTP-only.</summary>
public sealed class SubmitControlEvaluationHandler(IAggregateExecutor executor,
    TimeProvider clock)
    : IRequestHandler<SubmitControlEvaluation, ControlEvaluationView>
{
    public async ValueTask<Result<ControlEvaluationView>> HandleAsync(
        IRequestContext<SubmitControlEvaluation> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Result<ControlEvaluationView>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Control evaluation sign-off requires personal HTTP submission."));
        var request = context.Request;
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var now = clock.GetUtcNow();
        return await ControlEvaluationSource.ExecuteAsync(executor, context, request.TenantId,
            request.ProgramId, request.ControlId, request.EvaluationId,
            ledger => ledger.Submit(request.ControlId, request.EvaluationId,
                request.ExpectedRevision, request.Conclusions, actor.MemberId, actor.Display, now),
            ct).ConfigureAwait(false);
    }
}
