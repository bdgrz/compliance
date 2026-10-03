using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>Revises open accountable treatment work. HTTP-only.</summary>
[Discriminator("bdgrz.risk.treatment_action.revise", 1)]
public sealed record ReviseRiskTreatmentAction(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    Uuid ActionId, long ExpectedRevision, string Title, string TargetState,
    string ExpectedEvidence, DateOnly DueOn, Uuid AccountableMemberId,
    IReadOnlyList<Uuid>? EvidenceRequestIds = null)
    : IRequest, IProgramScopedRequest, ICallable;
