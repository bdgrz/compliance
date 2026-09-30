using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>An approved, effective-dated access-review scope decision for one exact instance revision.</summary>
[Discriminator("bdgrz.system_instance.access_review_scope.decided", 1)]
public sealed record AccessReviewScopeDecided(Uuid TenantId, Uuid ApplicationId,
    Uuid SystemInstanceId, long SystemInstanceRevision, Uuid DecisionId, long Sequence,
    string Decision, string Reason, DateTimeOffset EffectiveFrom, DateTimeOffset? ReviewBy,
    Uuid ApproverMemberId, string ApproverDisplay, DateTimeOffset DecidedAt,
    Uuid? SeparationOfDutiesWaiverId) : DomainEvent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(
        ApproverMemberId, ApproverDisplay);
}
