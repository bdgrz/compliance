using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application.relationship.removed", 1)]
public sealed record ApplicationRelationshipRemoved(Uuid TenantId, Uuid ApplicationId,
    Uuid RelationshipId, long RelationshipRevision, Uuid TargetApplicationId,
    string RelationshipType, Uuid ActorMemberId, string ActorDisplay,
    DateTimeOffset ChangedAt) : DomainEvent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(
        ActorMemberId, ActorDisplay);
}
