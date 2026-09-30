using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

[Discriminator("bdgrz.risk.method.version.get", 1)]
public sealed record GetRiskMethodVersion(Uuid TenantId, Uuid ProgramId, long Version)
    : IRequest<RiskMethodVersionView>, IProgramScopedRequest, ICallable;
