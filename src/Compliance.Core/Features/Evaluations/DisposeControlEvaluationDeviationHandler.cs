using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>Records a minor deviation's disposition; a waiver must be approved, active, and scoped to the deviation.</summary>
public sealed class DisposeControlEvaluationDeviationHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<DisposeControlEvaluationDeviation, ControlEvaluationView>
{
    public async ValueTask<Result<ControlEvaluationView>> HandleAsync(
        IRequestContext<DisposeControlEvaluationDeviation> context, CancellationToken ct)
    {
        var request = context.Request;
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var now = clock.GetUtcNow();
        SeparationOfDutiesWaiver? waiver = null;
        if (request.WaiverId is { } waiverId)
            waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId,
                waiverId), ct).ConfigureAwait(false);
        return await ControlEvaluationSource.ExecuteAsync(executor, context, request.TenantId,
            request.ProgramId, request.ControlId, request.EvaluationId,
            ledger => ledger.DisposeDeviation(request.ControlId, request.EvaluationId,
                request.DeviationId, request.ExpectedRevision, request.Disposition,
                request.Rationale, waiver, actor.MemberId, now),
            ct).ConfigureAwait(false);
    }
}
