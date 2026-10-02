using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

[Discriminator("bdgrz.risk.treatment_action.added", 1)]
public sealed record RiskTreatmentActionAdded(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long Revision, RiskTreatmentActionView Action) : DomainEvent;
