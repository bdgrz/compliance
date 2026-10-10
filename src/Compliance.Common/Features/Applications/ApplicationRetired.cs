using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>An effective-dated retirement. History, instances, and snapshot references remain.</summary>
[Discriminator("bdgrz.application.retired", 1)]
public sealed record ApplicationRetired(Uuid TenantId, Uuid ApplicationId, long Revision,
    DateTimeOffset EffectiveAt, string Reason, Uuid? MergedIntoApplicationId,
    Uuid ActorMemberId, string ActorDisplay, DateTimeOffset ChangedAt,
    long? MergedIntoApplicationRevision = null) : DomainEvent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(
        ActorMemberId, ActorDisplay);
}
