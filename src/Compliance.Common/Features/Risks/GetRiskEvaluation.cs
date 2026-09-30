using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

[Discriminator("bdgrz.risk.evaluation.get", 1)]
public sealed record GetRiskEvaluation(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long? MinimumRevision = null)
    : IRequest<RiskEvaluationView>, IProgramScopedRequest, ICallable;
