using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

/// <summary>An independent review decision; an accepted review creates an immutable effective version.</summary>
[Discriminator("bdgrz.commitment.reviewed", 1)]
public sealed record CommitmentReviewed(Uuid TenantId, Uuid ProgramId, Uuid DraftId,
    long Revision, Uuid DecisionId, string Outcome, string? OwnerReference,
    string? Applicability, string? Interpretation, string? InterpretationNote,
    string Rationale, long? Version, DateOnly? EffectiveFrom, string? ImpactDigest,
    Uuid ActorMemberId, string ActorDisplay, DateTimeOffset DecidedAt,
    Uuid? SeparationOfDutiesWaiverId = null) : DomainEvent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(ActorMemberId, ActorDisplay);
}
