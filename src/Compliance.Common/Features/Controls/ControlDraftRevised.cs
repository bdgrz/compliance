using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

[Discriminator("bdgrz.control.draft.revised", 1)]
public sealed record ControlDraftRevised(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    long Revision, ControlDraftContent Content, Uuid ActorMemberId,
    string ActorDisplay, DateTimeOffset ChangedAt) : DomainEvent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(ActorMemberId, ActorDisplay);
}
