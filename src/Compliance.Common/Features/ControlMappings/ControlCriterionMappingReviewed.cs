using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

[Discriminator("bdgrz.control_mapping.reviewed", 1)]
public sealed record ControlCriterionMappingReviewed(Uuid TenantId, Uuid ProgramId,
    Uuid MappingId, long Revision, int VersionNumber, Uuid DecisionId, string Outcome,
    string Rationale, Uuid ActorMemberId, string ActorDisplay, DateTimeOffset DecidedAt,
    Uuid? SeparationOfDutiesWaiverId = null) : DomainEvent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(
        ActorMemberId, ActorDisplay);
}
