using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application.relationship.recorded", 1)]
public sealed record ApplicationRelationshipRecorded(Uuid TenantId, Uuid RelationshipId,
    Uuid SourceApplicationId, Uuid TargetApplicationId, string RelationshipType,
    long Revision, string Status, long SourceApplicationRevision,
    long TargetApplicationRevision, Uuid ActorMemberId, string ActorDisplay,
    DateTimeOffset ChangedAt) : DomainEvent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(ActorMemberId, ActorDisplay);
}
