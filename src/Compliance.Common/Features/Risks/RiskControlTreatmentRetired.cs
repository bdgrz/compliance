using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

[Discriminator("bdgrz.risk.control_treatment.retired", 1)]
public sealed record RiskControlTreatmentRetired(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long Revision, Uuid TreatmentId, string Rationale, ActorReference RetiredBy,
    DateTimeOffset RetiredAt) : DomainEvent;
