using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

/// <summary>A periodic review that confirmed the current version without changing it.</summary>
[Discriminator("bdgrz.policy.periodic_review.confirmed", 1)]
public sealed record PolicyPeriodicReviewConfirmed(Uuid TenantId, Uuid ProgramId,
    Uuid PolicyId, long Version, Uuid DecisionId, DateOnly ReviewedOn, string Rationale,
    ActorReference Actor, Uuid ActorMemberId, DateTimeOffset DecidedAt) : DomainEvent;
