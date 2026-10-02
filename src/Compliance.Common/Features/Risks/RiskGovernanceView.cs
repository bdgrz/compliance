using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     A risk's owner, control treatment assertions, treatment actions, and reassessment
///     triggers. Revision orders governance writes on this risk and is the expected revision for
///     the next one. <c>TreatmentActionStatus</c> is <c>none</c>, <c>in_progress</c>,
///     <c>overdue</c>, or <c>completed</c>; it is <c>completed</c> only when every action has an
///     independently accepted completion backed by fulfilled evidence.
/// </summary>
public sealed record RiskGovernanceView(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long Revision, RiskOwnerView? Owner,
    IReadOnlyList<RiskControlTreatmentView> ControlTreatments,
    IReadOnlyList<RiskReassessmentTriggerView> ReassessmentTriggers,
    IReadOnlyList<RiskTreatmentActionView> TreatmentActions, string TreatmentActionStatus);
