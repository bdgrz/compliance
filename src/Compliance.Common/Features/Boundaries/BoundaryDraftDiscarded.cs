using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

[Discriminator("bdgrz.boundary.draft.discarded", 1)]
public sealed record BoundaryDraftDiscarded(Uuid TenantId, Uuid BoundaryId,
    Uuid DraftVersionId, long Revision, Uuid ActorMemberId,
    string ActorDisplay, string Rationale, DateTimeOffset DiscardedAt) : DomainEvent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(
        ActorMemberId, ActorDisplay);
}
