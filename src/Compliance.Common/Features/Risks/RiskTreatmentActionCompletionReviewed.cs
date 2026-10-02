using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

[Discriminator("bdgrz.risk.treatment_action.completion_reviewed", 1)]
public sealed record RiskTreatmentActionCompletionReviewed(Uuid TenantId, Uuid ProgramId,
    Uuid RiskId, long Revision, Uuid ActionId, Uuid DecisionId, string Outcome,
    string Rationale, ActorReference Reviewer, DateTimeOffset ReviewedAt,
    Uuid? SeparationOfDutiesWaiverId) : DomainEvent;
