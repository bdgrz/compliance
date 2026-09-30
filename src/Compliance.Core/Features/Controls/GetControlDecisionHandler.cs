using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public sealed class GetControlDecisionHandler(ControlActivationSource source)
    : IRequestHandler<GetControlDecision, ControlDecisionView>
{
    public async ValueTask<Result<ControlDecisionView>> HandleAsync(
        IRequestContext<GetControlDecision> context, CancellationToken ct)
    {
        var request = context.Request;
        var control = await source.LoadAsync(request.TenantId, request.ProgramId,
            request.ControlId, ct).ConfigureAwait(false);
        if (!control.IsSuccess)
            return Result<ControlDecisionView>.Failure(control.Error);
        var decision = control.Value.ReadDecisions()
            .FirstOrDefault(decision => decision.DecisionId == request.DecisionId);
        return decision is not null
            ? Result<ControlDecisionView>.Success(decision)
            : Result<ControlDecisionView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The control decision was not found."));
    }
}
