using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

[Discriminator("bdgrz.criterion_applicability.reviewed", 1)]
public sealed record CriterionApplicabilityReviewed(Uuid TenantId, Uuid ProgramId,
    Uuid DecisionId, long Revision, int VersionNumber, Uuid ReviewDecisionId, string Outcome,
    string Rationale, ActorReference Actor, DateTimeOffset ReviewedAt,
    Uuid? SeparationOfDutiesWaiverId) : DomainEvent;
