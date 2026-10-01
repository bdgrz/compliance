using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>Records the acting member's attributed feedback on a readiness gap; HTTP-only.</summary>
public sealed class AnnotateReadinessGapHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<AnnotateReadinessGap, ReadinessAnnotationView>
{
    public async ValueTask<Result<ReadinessAnnotationView>> HandleAsync(
        IRequestContext<AnnotateReadinessGap> context, CancellationToken ct)
    {
        var request = context.Request;
        var actor = ReadinessActor.From(context, request.TenantId);
        return await executor.ExecuteAsync(new ReadinessLedger(request.TenantId,
                request.ProgramId),
            ledger =>
            {
                var failure = ledger.Annotate(request.AssessmentId, request.GapId,
                    request.ExpectedRevision, context.RequestId, request.Body,
                    actor.MemberId, actor.Display, clock.GetUtcNow());
                return CommandFailureRequestAdapter.ToOutcome(failure,
                    ledger.FindAnnotation(context.RequestId)!);
            }, context, ct).ConfigureAwait(false);
    }
}
