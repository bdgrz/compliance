using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>Activates an immutable control version from one exact reviewed draft revision.</summary>
[Discriminator("bdgrz.control.approved", 1)]
public sealed record ControlApproved(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid VersionId, long Revision, Uuid ApprovalDecisionId, Uuid AcceptedReviewDecisionId,
    ControlDraftContent Content, Uuid OwnerAssignmentId, Uuid OwnerMemberId,
    Uuid ActorMemberId, string ActorDisplay, string Rationale, DateOnly EffectiveFrom,
    DateTimeOffset DecidedAt, Uuid? SeparationOfDutiesWaiverId = null,
    Uuid? PredecessorVersionId = null, string? ImpactDigest = null,
    Uuid? OwnerPersonId = null) : DomainEvent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(
        ActorMemberId, ActorDisplay);
}
