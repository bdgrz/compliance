using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>
///     Opens a successor version line from one exact approved version. The successor's authored
///     content is recorded by the <see cref="ControlDraftRevised" /> raised in the same commit.
/// </summary>
[Discriminator("bdgrz.control.successor.proposed", 1)]
public sealed record ControlSuccessorProposed(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid VersionId, Uuid PredecessorVersionId, long Revision, Uuid ActorMemberId,
    string ActorDisplay, DateTimeOffset ChangedAt) : DomainEvent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(ActorMemberId,
        ActorDisplay);
}
