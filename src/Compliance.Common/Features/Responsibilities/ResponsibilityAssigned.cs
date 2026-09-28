using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Responsibilities;

[Discriminator("bdgrz.responsibility.assigned", 1)]
public sealed record ResponsibilityAssigned(Uuid TenantId, Uuid AssignmentId,
    Uuid MemberId, ResponsibilityType Type, ResponsibilityScope Scope,
    DateTimeOffset AssignedAt, Uuid AssignedByMemberId,
    DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveUntil,
    IReadOnlyList<Uuid> SeparationOfDutiesWaiverIds, string AssignedByDisplay) : DomainEvent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(
        AssignedByMemberId, AssignedByDisplay);
}
