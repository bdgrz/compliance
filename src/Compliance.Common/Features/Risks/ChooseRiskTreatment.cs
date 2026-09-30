using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

[Discriminator("bdgrz.risk.treatment.choose", 1)]
public sealed record ChooseRiskTreatment(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long ExpectedRevision, string Kind, string Rationale)
    : IRequest, IProgramScopedRequest, ICallable;
