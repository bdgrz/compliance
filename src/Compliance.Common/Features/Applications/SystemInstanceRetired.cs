using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>An effective-dated instance retirement. Scope decisions and history remain.</summary>
[Discriminator("bdgrz.system_instance.retired", 1)]
public sealed record SystemInstanceRetired(Uuid TenantId, Uuid ApplicationId,
    Uuid SystemInstanceId, long Revision, DateTimeOffset EffectiveAt, string Reason,
    Uuid ActorMemberId, string ActorDisplay, DateTimeOffset ChangedAt) : DomainEvent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(
        ActorMemberId, ActorDisplay);
}
