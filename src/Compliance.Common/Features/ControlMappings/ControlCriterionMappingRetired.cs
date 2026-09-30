using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

[Discriminator("bdgrz.control_mapping.retired", 1)]
public sealed record ControlCriterionMappingRetired(Uuid TenantId, Uuid ProgramId,
    Uuid MappingId, long Revision, int VersionNumber, string Rationale, Uuid ActorMemberId,
    string ActorDisplay, DateTimeOffset RetiredAt) : DomainEvent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(
        ActorMemberId, ActorDisplay);
}
