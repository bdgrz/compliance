using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

/// <summary>A separate approval of an accepted review; it creates an immutable effective version.</summary>
[Discriminator("bdgrz.commitment.approved", 1)]
public sealed record CommitmentApproved(Uuid TenantId, Uuid ProgramId, Uuid DraftId,
    long Revision, Uuid DecisionId, Uuid AcceptedReviewDecisionId, long Version,
    DateOnly EffectiveFrom, string ImpactDigest, string Rationale, Uuid ActorMemberId,
    string ActorDisplay, DateTimeOffset DecidedAt, Uuid? SeparationOfDutiesWaiverId = null)
    : DomainEvent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(ActorMemberId, ActorDisplay);
}
