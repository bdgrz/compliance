using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

[Discriminator("bdgrz.risk.control_treatment.reviewed", 1)]
public sealed record RiskControlTreatmentReviewed(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long Revision, Uuid TreatmentId, Uuid DecisionId, string Outcome, string Rationale,
    ActorReference Reviewer, DateTimeOffset ReviewedAt, Uuid? SeparationOfDutiesWaiverId)
    : DomainEvent;
