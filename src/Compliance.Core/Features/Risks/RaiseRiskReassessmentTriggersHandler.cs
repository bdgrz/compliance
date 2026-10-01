using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     Raises a durable trigger for every assessed risk of the program. A method change skips
///     risks already assessed on the new method version. Trigger identity is stable, so a
///     replayed reaction appends nothing. The risk list must have reached its source, so a lagging
///     projection fails transiently and the reaction retries instead of skipping a new risk.
/// </summary>
public sealed class RaiseRiskReassessmentTriggersHandler(IAggregateExecutor executor,
    IAggregateReader reader, IRiskDraftDirectoryReader risks,
    RiskDraftListReadConsistency consistency)
    : IRequestHandler<RaiseRiskReassessmentTriggers>
{
    public async ValueTask<Result> HandleAsync(
        IRequestContext<RaiseRiskReassessmentTriggers> context, CancellationToken ct)
    {
        var request = context.Request;
        var caughtUp = await consistency.EnsureCaughtUpAsync(request.TenantId, ct)
            .ConfigureAwait(false);
        if (!caughtUp.IsSuccess)
            return caughtUp;
        var affected = new List<Uuid>();
        string? cursor = null;
        do
        {
            var page = await risks.ListProgramAsync(request.TenantId, request.ProgramId, 200,
                cursor, ct).ConfigureAwait(false);
            foreach (var risk in page.Items.Where(item => item.TenantId == request.TenantId &&
                         item.ProgramId == request.ProgramId))
            {
                var evaluation = await reader.HydrateAsync(new RiskEvaluation(request.TenantId,
                    risk.RiskId), ct).ConfigureAwait(false);
                if (evaluation.Latest(RiskEvaluation.Inherent) is not { } inherent)
                    continue;
                if (request.MethodVersionId is { } method && inherent.MethodVersionId == method)
                    continue;
                affected.Add(risk.RiskId);
            }
            cursor = page.NextCursor;
        } while (cursor is not null);
        if (affected.Count == 0)
            return Result.Success;
        return await executor.ExecuteAsync(new RiskGovernanceLedger(request.TenantId,
                request.ProgramId),
            ledger =>
            {
                foreach (var riskId in affected)
                    ledger.RaiseTrigger(riskId, request.TriggerKind, request.SourceReference,
                        request.RaisedAt);
                return AggregateOutcome.Commit(Result.Success);
            }, context, ct).ConfigureAwait(false);
    }
}
