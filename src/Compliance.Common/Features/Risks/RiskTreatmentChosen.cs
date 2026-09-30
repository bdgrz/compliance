using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

[Discriminator("bdgrz.risk.treatment.chosen", 1)]
public sealed record RiskTreatmentChosen(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long Revision, RiskTreatmentView Treatment) : DomainEvent;
