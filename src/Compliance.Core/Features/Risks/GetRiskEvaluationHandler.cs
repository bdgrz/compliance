using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public sealed class GetRiskEvaluationHandler(RiskEvaluationReadConsistency consistency,
    TimeProvider clock) : IRequestHandler<GetRiskEvaluation, RiskEvaluationView>
{
    public async ValueTask<Result<RiskEvaluationView>> HandleAsync(
        IRequestContext<GetRiskEvaluation> context, CancellationToken ct)
    {
        var request = context.Request;
        var view = await consistency.GetAsync(request.TenantId, request.ProgramId,
            request.RiskId, request.MinimumRevision, ct).ConfigureAwait(false);
        return view.IsSuccess
            ? Result<RiskEvaluationView>.Success(RiskEvaluationStatus.AsOf(view.Value,
                clock.GetUtcNow()))
            : view;
    }
}
