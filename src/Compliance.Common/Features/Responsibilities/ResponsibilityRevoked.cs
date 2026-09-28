using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Responsibilities;

[Discriminator("bdgrz.responsibility.revoked", 1)]
public sealed record ResponsibilityRevoked(Uuid TenantId, Uuid AssignmentId,
    ResponsibilityScope Scope, Uuid RevokedByMemberId, DateTimeOffset RevokedAt, string Reason,
    string RevokedByDisplay) : DomainEvent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(
        RevokedByMemberId, RevokedByDisplay);
}
