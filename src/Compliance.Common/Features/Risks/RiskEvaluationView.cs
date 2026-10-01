using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public sealed record RiskEvaluationView(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long Revision, string Status, IReadOnlyList<RiskAssessmentView> Assessments,
    RiskTreatmentView? Treatment, IReadOnlyList<RiskAcceptanceView> Acceptances,
    DateTimeOffset? ReassessmentDueAt, ActorReference? LastChangedBy,
    DateTimeOffset? LastChangedAt)
{
    /// <summary>Open M0-D10 reassessment triggers, evaluated when read; never projected.</summary>
    public IReadOnlyList<RiskReassessmentTriggerView>? OpenReassessmentTriggers { get; init; }
}
