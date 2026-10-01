using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>
///     Records the independent reviewer's decision; HTTP-only. The reviewer must manage the
///     program (Compliance Lead or Org Admin) and must not be the evaluator unless an approved,
///     active waiver scoped to this evaluation round applies.
/// </summary>
public sealed class ReviewControlEvaluationHandler(IAggregateExecutor executor,
    IAggregateReader reader, OperatingAuthority authority, TimeProvider clock)
    : IRequestHandler<ReviewControlEvaluation, ControlEvaluationView>
{
    public async ValueTask<Result<ControlEvaluationView>> HandleAsync(
        IRequestContext<ReviewControlEvaluation> context, CancellationToken ct)
    {
        var request = context.Request;
        var current = await reader.HydrateAsync(new ControlEvaluationLedger(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        if (current.Read(request.ControlId, request.EvaluationId) is null)
            return Result<ControlEvaluationView>.Failure(ControlEvaluationSource.EvaluationNotFound());
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        if (!await authority.ManagesProgramAsync(request.TenantId, actor, request.ProgramId, ct)
                .ConfigureAwait(false))
            return Result<ControlEvaluationView>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only a Compliance Lead or Org Admin for the program may review an evaluation."));
        SeparationOfDutiesWaiver? waiver = null;
        if (request.SeparationOfDutiesWaiverId is { } waiverId)
            waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId,
                waiverId), ct).ConfigureAwait(false);
        var now = clock.GetUtcNow();
        return await ControlEvaluationSource.ExecuteAsync(executor, context, request.TenantId,
            request.ProgramId, request.ControlId, request.EvaluationId,
            ledger => ledger.Review(request.ControlId, request.EvaluationId,
                request.ExpectedRevision, context.RequestId, request.Decision, request.Rationale,
                actor.MemberId, actor.Display, now, waiver), ct).ConfigureAwait(false);
    }
}
