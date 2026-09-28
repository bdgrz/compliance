using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

[Discriminator("bdgrz.boundary.approved", 1)]
public sealed record BoundaryApproved(Uuid TenantId, Uuid BoundaryId, Uuid DraftVersionId,
    long Revision, Uuid ApprovalDecisionId, Uuid AcceptedReviewDecisionId, Uuid ActorMemberId,
    string ActorDisplay, string Rationale, DateOnly EffectiveFrom,
    DateTimeOffset DecidedAt, string ImpactDigest,
    Uuid? SeparationOfDutiesWaiverId = null) : DomainEvent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(
        ActorMemberId, ActorDisplay);
}
