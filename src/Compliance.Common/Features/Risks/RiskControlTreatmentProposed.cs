using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

[Discriminator("bdgrz.risk.control_treatment.proposed", 1)]
public sealed record RiskControlTreatmentProposed(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long Revision, RiskControlTreatmentView Treatment, Uuid ProposerMemberId) : DomainEvent;
