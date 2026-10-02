using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     Cancels unfinished treatment work while retaining its history. HTTP-only.
/// </summary>
[Discriminator("bdgrz.risk.treatment_action.cancel", 1)]
public sealed record CancelRiskTreatmentAction(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    Uuid ActionId, long ExpectedRevision, string Rationale)
    : IRequest, IProgramScopedRequest, ICallable;
