using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>Records a minor deviation's disposition; a waiver must be approved and unexpired.</summary>
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
        var waiverActive = false;
        if (request.WaiverId is { } waiverId)
        {
            var waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId,
                waiverId), ct).ConfigureAwait(false);
            waiverActive = waiver.IsRecorded && waiver.TenantId == request.TenantId &&
                           waiver.ApprovedAt is not null && waiver.ExpiresAt > now;
        }
        return await ControlEvaluationSource.ExecuteAsync(executor, context, request.TenantId,
            request.ProgramId, request.ControlId, request.EvaluationId,
            ledger => ledger.DisposeDeviation(request.ControlId, request.EvaluationId,
                request.DeviationId, request.ExpectedRevision, request.Disposition,
                request.Rationale, request.WaiverId, waiverActive, actor.MemberId),
            ct).ConfigureAwait(false);
    }
}
