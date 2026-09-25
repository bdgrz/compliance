using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

[Discriminator("bdgrz.client-service.created", 1)]
public sealed record ClientServiceCreated(Uuid TenantId, Uuid ServiceId, string Name,
    string Purpose, string OwnerReference, Uuid ActorMemberId, string ActorDisplay,
    DateTimeOffset ChangedAt, Uuid ProgramId = default) : DomainEvent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(ActorMemberId, ActorDisplay);
}
