using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

[Discriminator("bdgrz.risk.method.publish", 1)]
public sealed record PublishRiskMethodVersion(Uuid TenantId, Uuid ProgramId,
    long ExpectedVersion, IReadOnlyList<string> LikelihoodScale,
    IReadOnlyList<string> ImpactScale, int? AppetiteThreshold = null)
    : IRequest<RiskMethodVersionView>, IProgramScopedRequest, ICallable;
