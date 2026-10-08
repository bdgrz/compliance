using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class EvaluateClientIndependenceHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<EvaluateClientIndependence, IndependenceEvaluationView>
{
    public async ValueTask<Result<IndependenceEvaluationView>> HandleAsync(
        IRequestContext<EvaluateClientIndependence> context, CancellationToken ct)
    {
        var request = context.Request;
        var catalog = await reader.HydrateAsync(new IndependenceRuleCatalog(), ct).ConfigureAwait(false);
        if (catalog.Current is not { } version || version.Version != request.RuleSetVersion)
            return Stale();
        var observedSequence = catalog.Sequence;
        var result = await executor.ExecuteAsync(new IndependenceLedger(request.TenantId), ledger =>
            AggregateOutcome.CommitOnSuccess(ledger.Evaluate(context.RequestId, request.EvaluationId,
                request.ExpectedSequence, version, request.ExaminationPeriodStart,
                AccessGrantActor.From(context, request.TenantId), clock.GetUtcNow())), context, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
            return result;
        var latest = await reader.HydrateAsync(new IndependenceRuleCatalog(), ct).ConfigureAwait(false);
        return latest.Sequence == observedSequence ? result : Stale();
    }

    static Result<IndependenceEvaluationView> Stale() => Result<IndependenceEvaluationView>.Failure(
        new RequestError(RequestErrorKind.Conflict,
            "The draft rule source changed. Reload its exact version before evaluating. Retained previews never grant acceptance.",
            isTransient: true));
}
