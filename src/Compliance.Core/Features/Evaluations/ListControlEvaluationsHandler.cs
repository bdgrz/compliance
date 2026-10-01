using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>Lists a control's evaluations, newest first, keeping every prior decision visible.</summary>
public sealed class ListControlEvaluationsHandler(IAggregateReader reader)
    : IRequestHandler<ListControlEvaluations, Page<ControlEvaluationView>>
{
    public async ValueTask<Result<Page<ControlEvaluationView>>> HandleAsync(
        IRequestContext<ListControlEvaluations> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.State is not (null or ControlEvaluationLedger.InProgress or
            ControlEvaluationLedger.Submitted or ControlEvaluationLedger.Accepted))
            return Result<Page<ControlEvaluationView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The state filter must be in_progress, submitted, or accepted."));
        var control = await ControlOperationsSource.LoadControlAsync(reader, request.TenantId,
            request.ProgramId, request.ControlId, ct).ConfigureAwait(false);
        if (control is null)
            return Result<Page<ControlEvaluationView>>.Failure(
                ControlOperationsSource.ControlNotFound());
        var ledger = await reader.HydrateAsync(new ControlEvaluationLedger(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        var remediation = await reader.HydrateAsync(new RemediationLedger(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        var items = ledger.ReadControl(request.ControlId)
            .Where(view => request.State is null || view.State == request.State)
            .Select(view => ControlEvaluationSource.WithRouting(view, remediation)).ToArray();
        return ControlActivationSource.Paginate(items, request.Limit, request.Cursor,
            "control evaluations");
    }
}
