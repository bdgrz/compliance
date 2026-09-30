using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

[Discriminator("bdgrz.risk.method.get", 1)]
public sealed record GetRiskMethod(Uuid TenantId, Uuid ProgramId)
    : IRequest<RiskMethodVersionView>, IProgramScopedRequest, ICallable;
