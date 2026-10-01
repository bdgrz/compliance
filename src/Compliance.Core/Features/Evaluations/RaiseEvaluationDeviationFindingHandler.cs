using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>
///     Raises the owned finding and corrective action for a submitted material deviation. The
///     control version's owner member (or the evaluator when the owner does not sign in) owns it,
///     due in 30 days at medium severity, matching the occurrence-finding product default.
/// </summary>
public sealed class RaiseEvaluationDeviationFindingHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<RaiseEvaluationDeviationFinding>
{
    public const string ProcessId = "reactor:" + ControlEvaluationDeviationFindingReactor.WorkloadName;

    public async ValueTask<Result> HandleAsync(
        IRequestContext<RaiseEvaluationDeviationFinding> context, CancellationToken ct)
    {
        var request = context.Request;
        var evaluations = await reader.HydrateAsync(new ControlEvaluationLedger(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        if (evaluations.Read(request.ControlId, request.EvaluationId) is not { } evaluation ||
            evaluations.SubmittedDeviation(request.ControlId, request.EvaluationId,
                request.DeviationId) is not { Classification: ControlEvaluationLedger.Material } deviation ||
            deviation.FindingId != request.FindingId)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The submitted material deviation was not found."));
        var control = await reader.HydrateAsync(new ControlDraft(request.TenantId,
            request.ControlId), ct).ConfigureAwait(false);
        var owner = control.ReadVersions().FirstOrDefault(version =>
            version.VersionId == evaluation.ControlVersionId)?.OwnerMemberId is { } member &&
                    member != Uuid.Empty
            ? member
            : evaluation.EvaluatorMemberId;
        var now = clock.GetUtcNow();
        var dueOn = DateOnly.FromDateTime(now.UtcDateTime)
            .AddDays(RaiseOccurrenceFindingHandler.DefaultDueDays);
        var actor = ActorReference.ForSystemProcess(ProcessId,
            ControlEvaluationDeviationFindingReactor.WorkloadName);
        return await executor.ExecuteAsync(new RemediationLedger(request.TenantId,
                request.ProgramId),
            ledger =>
            {
                if (ledger.Contains(request.FindingId))
                    return CommandFailureRequestAdapter.ToOutcome(null);
                var failure = ledger.Raise(request.FindingId,
                    new FindingSource("evaluation_deviation", deviation.DeviationId,
                        request.EvaluationId.ToString(), deviation.Description),
                    "Material control evaluation deviation", deviation.Description,
                    RaiseOccurrenceFindingHandler.DefaultSeverity,
                    $"Control {request.ControlId}, evaluation {request.EvaluationId}", owner, dueOn,
                    [new FindingLink("control", request.ControlId.ToString()),
                        new FindingLink("control_evaluation", request.EvaluationId.ToString())],
                    actor, now);
                return CommandFailureRequestAdapter.ToOutcome(failure ?? ledger.AddAction(
                    request.FindingId, 1, Uuid.CreateVersion5(request.FindingId,
                        "corrective-action"), deviation.Description, owner, dueOn, actor, now));
            }, context, ct).ConfigureAwait(false);
    }
}
