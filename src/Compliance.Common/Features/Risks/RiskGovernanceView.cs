using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     A risk's owner, control treatment assertions, and reassessment triggers. Revision orders
///     governance writes on this risk and is the expected revision for the next one.
/// </summary>
public sealed record RiskGovernanceView(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long Revision, RiskOwnerView? Owner,
    IReadOnlyList<RiskControlTreatmentView> ControlTreatments,
    IReadOnlyList<RiskReassessmentTriggerView> ReassessmentTriggers);
