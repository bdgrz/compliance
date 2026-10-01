using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>Reads one evaluation with its frozen procedure, every submission, and every review.</summary>
public sealed class GetControlEvaluationHandler(IAggregateReader reader)
    : IRequestHandler<GetControlEvaluation, ControlEvaluationView>
{
    public async ValueTask<Result<ControlEvaluationView>> HandleAsync(
        IRequestContext<GetControlEvaluation> context, CancellationToken ct)
    {
        var request = context.Request;
        if (await ControlOperationsSource.LoadControlAsync(reader, request.TenantId,
                request.ProgramId, request.ControlId, ct).ConfigureAwait(false) is null)
            return Result<ControlEvaluationView>.Failure(ControlOperationsSource.ControlNotFound());
        var ledger = await reader.HydrateAsync(new ControlEvaluationLedger(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        if (ledger.Read(request.ControlId, request.EvaluationId) is not { } view)
            return Result<ControlEvaluationView>.Failure(ControlEvaluationSource.EvaluationNotFound());
        var remediation = await reader.HydrateAsync(new RemediationLedger(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        return Result<ControlEvaluationView>.Success(
            ControlEvaluationSource.WithRouting(view, remediation));
    }
}
