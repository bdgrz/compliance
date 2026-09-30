using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>
///     Ends future use of one exact approved control version. Prior versions, decisions, and
///     relationships are retained and stay readable.
/// </summary>
[Discriminator("bdgrz.control.retired", 1)]
public sealed record ControlRetired(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid RetirementId, Uuid VersionId, long Revision, Uuid DecisionId,
    Uuid AcceptedReviewDecisionId, DateOnly EffectiveUntil, string ImpactDigest,
    Uuid ActorMemberId, string ActorDisplay, string Rationale, DateTimeOffset DecidedAt,
    Uuid? SeparationOfDutiesWaiverId = null) : DomainEvent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(ActorMemberId,
        ActorDisplay);
}
