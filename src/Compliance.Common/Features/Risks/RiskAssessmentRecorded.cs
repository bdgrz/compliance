using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

[Discriminator("bdgrz.risk.assessment.recorded", 1)]
public sealed record RiskAssessmentRecorded(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long Revision, RiskAssessmentView Assessment) : DomainEvent;
