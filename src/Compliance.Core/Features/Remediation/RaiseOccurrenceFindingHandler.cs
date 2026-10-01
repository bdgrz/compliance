using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     Raises the finding for a failed or skipped attestation or a reviewer's requested action.
///     The control owner (or the recorder when the owner is not a member) owns it, due in 30 days
///     at medium severity as a product default the compliance lead may revise. A requested action
///     also becomes the finding's corrective action. Replays are idempotent by finding ID.
/// </summary>
public sealed class RaiseOccurrenceFindingHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<RaiseOccurrenceFinding>
{
    public const int DefaultDueDays = RoutedFinding.DefaultDueDays;
    public const string DefaultSeverity = RoutedFinding.DefaultSeverity;
    public const string ProcessId = "reactor:" + ControlOccurrenceFindingReactor.WorkloadName;

    public async ValueTask<Result> HandleAsync(
        IRequestContext<RaiseOccurrenceFinding> context, CancellationToken ct)
    {
        var request = context.Request;
        var operations = await reader.HydrateAsync(new ControlOperationsLedger(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        if (operations.ReadOccurrence(request.ControlId, request.OccurrenceId,
                new Dictionary<Uuid, DateOnly?>(), today) is not { Revision: > 0 } occurrence ||
            occurrence.Attestations.Count == 0)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The control occurrence was not found."));
        var owner = operations.CurrentPlan(request.ControlId)?.Owner is { Kind: "member" } member
            ? member.Id
            : occurrence.Reviews.Count > 0
                ? occurrence.Reviews[^1].ReviewerMemberId
                : occurrence.Attestations[^1].RecorderMemberId;
        var actor = ActorReference.ForSystemProcess(ProcessId,
            ControlOccurrenceFindingReactor.WorkloadName);
        var isRequestedAction = request.SourceKind == "occurrence_review";
        return await RoutedFinding.RaiseAsync(executor, context, request.TenantId,
            request.ProgramId, request.FindingId,
            new FindingSource(request.SourceKind, request.OccurrenceId, request.SourceVersion,
                request.SourceText),
            isRequestedAction ? "Requested action from control review" : "Control occurrence exception",
            $"Control {request.ControlId}, occurrence {request.OccurrenceId}", owner,
            [new FindingLink("control", request.ControlId.ToString()),
                new FindingLink("control_occurrence", request.OccurrenceId.ToString())],
            isRequestedAction ? request.SourceText : null, actor, now, ct).ConfigureAwait(false);
    }
}
