using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

[Discriminator("bdgrz.risk.governance.get", 1)]
public sealed record GetRiskGovernance(Uuid TenantId, Uuid ProgramId, Uuid RiskId)
    : IRequest<RiskGovernanceView>, IProgramScopedRequest, ICallable;
