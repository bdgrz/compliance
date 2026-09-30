using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>One attributable, immutable change to a risk's evaluation.</summary>
public sealed record RiskEvaluationHistoryEntryView(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long Revision, string Kind, RiskAssessmentView? Assessment, RiskTreatmentView? Treatment,
    RiskAcceptanceView? Acceptance, ActorReference Actor, DateTimeOffset OccurredAt);
