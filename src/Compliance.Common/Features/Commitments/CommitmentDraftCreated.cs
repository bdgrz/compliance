using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

[Discriminator("bdgrz.commitment.draft.created", 1)]
public sealed record CommitmentDraftCreated(Uuid TenantId, Uuid ProgramId, Uuid DraftId,
    Uuid CreateRequestId, Uuid ServiceId, string Kind, string Identifier,
    string Statement, string Context, string SourceReference,
    Uuid ActorMemberId, string ActorDisplay, DateTimeOffset ChangedAt) : DomainEvent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(ActorMemberId, ActorDisplay);
}
