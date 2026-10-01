using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

[Discriminator("bdgrz.policy.retired", 1)]
public sealed record PolicyRetired(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    long Revision, long Version, Uuid DecisionId, Uuid AcceptedReviewDecisionId,
    DateOnly EffectiveUntil, string Rationale, ActorReference Actor, Uuid ActorMemberId,
    DateTimeOffset DecidedAt, Uuid? SeparationOfDutiesWaiverId) : DomainEvent;
