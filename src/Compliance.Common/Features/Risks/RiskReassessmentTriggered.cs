using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

[Discriminator("bdgrz.risk.reassessment.triggered", 1)]
public sealed record RiskReassessmentTriggered(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long Revision, RiskReassessmentTriggerView Trigger) : DomainEvent;
