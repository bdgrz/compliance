using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     Reports that a treatment action reached its target state, citing at least one fulfilled
///     program evidence request. The action stays incomplete until an independent review accepts
///     it. HTTP-only.
/// </summary>
[Discriminator("bdgrz.risk.treatment_action.completion.submit", 1)]
public sealed record SubmitRiskTreatmentActionCompletion(Uuid TenantId, Uuid ProgramId,
    Uuid RiskId, Uuid ActionId, long ExpectedRevision, string Summary,
    IReadOnlyList<Uuid> EvidenceRequestIds)
    : IRequest<RiskTreatmentActionCompletionRegistration>, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
