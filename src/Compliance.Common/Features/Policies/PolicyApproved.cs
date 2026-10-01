using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

/// <summary>
///     Activates the exact reviewed draft revision as an immutable version identified by
///     <c>ContentSha256</c>. A major version requires every audience to re-acknowledge.
/// </summary>
[Discriminator("bdgrz.policy.approved", 1)]
public sealed record PolicyApproved(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    long Revision, long Version, bool Major, Uuid DecisionId, Uuid AcceptedReviewDecisionId,
    string ContentSha256, DateOnly EffectiveFrom, long? PredecessorVersion,
    string? ImpactDigest, string Rationale, ActorReference Actor, Uuid ActorMemberId,
    DateTimeOffset DecidedAt, Uuid? SeparationOfDutiesWaiverId) : DomainEvent;
