using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>Withdraws an accepted control treatment assertion while keeping its history.</summary>
[Discriminator("bdgrz.risk.control_treatment.retire", 1)]
public sealed record RetireRiskControlTreatment(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    Uuid TreatmentId, long ExpectedRevision, string Rationale)
    : IRequest, IProgramScopedRequest, ICallable;
