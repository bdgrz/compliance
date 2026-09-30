using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

[Discriminator("bdgrz.control_mapping.proposed", 1)]
public sealed record ControlCriterionMappingProposed(Uuid TenantId, Uuid ProgramId,
    Uuid MappingId, long Revision, int VersionNumber, Uuid ControlId, Uuid ControlVersionId,
    Uuid EditionId, string CriterionIdentifier, string CriterionKind, string Rationale,
    string ApplicabilityExplanation, Uuid ActorMemberId, string ActorDisplay,
    DateTimeOffset ProposedAt) : DomainEvent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(
        ActorMemberId, ActorDisplay);
}
