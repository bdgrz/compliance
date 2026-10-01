using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>Loads the control an evaluation request acts on and resolves deviation routing.</summary>
static class ControlEvaluationSource
{
    public static RequestError EvaluationNotFound() =>
        new(RequestErrorKind.NotFound, "The control evaluation was not found.");

    /// <summary>Marks submitted material deviations whose finding now exists as routed.</summary>
    public static ControlEvaluationView WithRouting(ControlEvaluationView view,
        RemediationLedger remediation)
    {
        static EvaluationDeviationView Route(EvaluationDeviationView deviation,
            RemediationLedger remediation) =>
            deviation.FindingId is { } findingId && remediation.Contains(findingId)
                ? deviation with { Status = ControlEvaluationLedger.RoutedToFinding }
                : deviation;

        return view with
        {
            Deviations = view.Deviations.Select(d => Route(d, remediation)).ToArray(),
            Submissions = view.Submissions.Select(submission => submission with
            {
                Deviations = submission.Deviations.Select(d => Route(d, remediation)).ToArray(),
            }).ToArray(),
        };
    }

    public static async ValueTask<Result<ControlEvaluationView>> ExecuteAsync<TRequest>(
        IAggregateExecutor executor, IRequestContext<TRequest> context, Uuid tenantId,
        Uuid programId, Uuid controlId, Uuid evaluationId,
        Func<ControlEvaluationLedger, CommandFailure?> command, CancellationToken ct)
        where TRequest : IRequestBase =>
        await executor.ExecuteAsync(new ControlEvaluationLedger(tenantId, programId), ledger =>
        {
            var failure = command(ledger);
            return CommandFailureRequestAdapter.ToOutcome(failure,
                ledger.Read(controlId, evaluationId)!);
        }, context, ct).ConfigureAwait(false);
}
