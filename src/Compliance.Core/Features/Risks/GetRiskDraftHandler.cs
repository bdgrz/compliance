using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public sealed class GetRiskDraftHandler(RiskDraftReadConsistency consistency)
    : IRequestHandler<GetRiskDraft, RiskDraftView>
{
    public ValueTask<Result<RiskDraftView>> HandleAsync(
        IRequestContext<GetRiskDraft> context, CancellationToken ct) =>
        consistency.GetAsync(context.Request.TenantId, context.Request.ProgramId,
            context.Request.RiskId, context.Request.MinimumRevision, ct);
}
