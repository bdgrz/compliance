using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     Asserts that an exact approved control version treats the risk under <c>mitigate</c>.
///     The assertion counts only after an independent review accepts it. HTTP-only.
/// </summary>
[Discriminator("bdgrz.risk.control_treatment.propose", 1)]
public sealed record ProposeRiskControlTreatment(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long ExpectedRevision, Uuid ControlId, Uuid ControlVersionId, string Rationale)
    : IRequest<RiskControlTreatmentRegistration>, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
