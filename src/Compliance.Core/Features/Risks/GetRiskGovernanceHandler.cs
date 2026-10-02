using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     Reads a risk's governance from the program ledger, its authoritative source. Treatment
///     action overdue state is evaluated at read time.
/// </summary>
public sealed class GetRiskGovernanceHandler(IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<GetRiskGovernance, RiskGovernanceView>
{
    public async ValueTask<Result<RiskGovernanceView>> HandleAsync(
        IRequestContext<GetRiskGovernance> context, CancellationToken ct)
    {
        var request = context.Request;
        var risk = await RiskOwnerResolution.RequireRiskAsync(reader, request.TenantId,
            request.ProgramId, request.RiskId, ct).ConfigureAwait(false);
        if (!risk.IsSuccess)
            return Result<RiskGovernanceView>.Failure(risk.Error);
        var ledger = await reader.HydrateAsync(new RiskGovernanceLedger(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        var evaluation = await reader.HydrateAsync(new RiskEvaluation(request.TenantId,
            request.RiskId), ct).ConfigureAwait(false);
        return Result<RiskGovernanceView>.Success(ledger.View(request.RiskId,
            evaluation.ToView().Assessments, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime)));
    }
}
